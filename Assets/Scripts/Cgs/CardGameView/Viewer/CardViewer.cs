/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System.Collections.Generic;
using CardGameDef;
using CardGameDef.Unity;
using Cgs.CardGameView.Multiplayer;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityExtensionMethods;

namespace Cgs.CardGameView.Viewer
{
    public enum CardViewerMode
    {
        Minimal = 0,
        Expanded = 1,
        Maximal = 2
    }

    public class CardViewer : MonoBehaviour, ICardDisplay, IPointerDownHandler, ISelectHandler, IDeselectHandler
    {
        private const string PlayerPrefsIsNameVisible = "IsNameVisible";

        private const string SetLabel = "Set";
        private const string IdLabel = "Id";
        private const string Delimiter = ": ";

        public static CardViewer Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var cardViewerGameObject = GameObject.FindWithTag(Tags.CardViewer);
                if (cardViewerGameObject != null)
                    _instance = cardViewerGameObject.GetOrAddComponent<CardViewer>();
                return _instance;
            }
        }

        private static CardViewer _instance;

        public CardViewerMode Mode
        {
            get => _mode;
            set
            {
                _mode = value;
                Redisplay();
            }
        }

        private CardViewerMode _mode;

        public CanvasGroup preview;
        public CanvasGroup minimal;
        public CanvasGroup expanded;
        public CanvasGroup maximal;

        public ScrollRect maximalScrollRect;

        public RectTransform zoomPanel;

        public Text previewNameText;
        public Text previewIdText;
        public List<AspectRatioFitter> cardAspectRatioFitters;
        public List<Image> cardImages;
        public List<Text> nameTexts;
        public List<Text> uniqueIdTexts;
        public Transform nameVisibleButton;
        public Image nameVisibleButtonImage;
        public Image nameInvisibleButtonImage;
        public Text idText;
        public Text setText;
        public Text propertyTextTemplate;
        public List<Dropdown> propertySelectors;
        public List<Text> propertyValueTexts;

        private List<Text> PropertyTexts { get; } = new List<Text>();
        private List<Dropdown.OptionData> PropertyOptions { get; } = new List<Dropdown.OptionData>();
        private Dictionary<string, string> DisplayNameLookup { get; } = new Dictionary<string, string>();

        private int PrimaryPropertyIndex
        {
            get
            {
                var primaryPropertyIndex = 0;
                for (var i = 0; i < PropertyOptions.Count; i++)
                    if (DisplayNameLookup.TryGetValue(PropertyOptions[i].text, out string propertyName)
                        && propertyName.Equals(CardGameManager.Current.CardPrimaryProperty))
                        primaryPropertyIndex = i;
                return primaryPropertyIndex;
            }
        }

        private string SelectedPropertyName
        {
            get
            {
                var selectedName = SetLabel;
                if (SelectedPropertyIndex == 1)
                    selectedName = IdLabel;
                if (SelectedPropertyIndex > 1 && SelectedPropertyIndex < PropertyOptions.Count)
                    DisplayNameLookup.TryGetValue(SelectedPropertyDisplay, out selectedName);
                return selectedName;
            }
        }

        private string SelectedPropertyDisplay
        {
            get
            {
                var selectedDisplay = SetLabel;
                if (SelectedPropertyIndex == 1)
                    selectedDisplay = IdLabel;
                if (SelectedPropertyIndex > 1 && SelectedPropertyIndex < PropertyOptions.Count)
                    selectedDisplay = PropertyOptions[SelectedPropertyIndex].text;
                return selectedDisplay;
            }
        }

        private int SelectedPropertyIndex
        {
            get => _selectedPropertyIndex;
            set
            {
                _selectedPropertyIndex = value;
                if (_selectedPropertyIndex < 0)
                    _selectedPropertyIndex = PropertyOptions.Count - 1;
                if (_selectedPropertyIndex >= PropertyOptions.Count)
                    _selectedPropertyIndex = 0;
                foreach (var propertySelector in propertySelectors)
                    propertySelector.value = _selectedPropertyIndex;
                ResetPropertyValueText();
            }
        }

        private int _selectedPropertyIndex;

        public CardModel SelectedCardModel
        {
            get => _selectedCardModel;
            set
            {
                if (_selectedCardModel != null)
                {
                    _selectedCardModel.HighlightMode = HighlightMode.Off;
                    _selectedCardModel.Value.UnregisterDisplay(this);
                }

                _selectedCardModel = value;

                if (_selectedCardModel != null)
                {
                    var selectedCard = _selectedCardModel.Value;
                    ResetTexts();
                    selectedCard.RegisterDisplay(this);
                }
                else if (!EventSystem.current.alreadySelecting)
                    EventSystem.current.SetSelectedGameObject(null);

                IsVisible = _selectedCardModel != null;
                ZoomTime = 0;
            }
        }

        private CardModel _selectedCardModel;

        public bool Zoom
        {
            get => zoomPanel.gameObject.activeSelf;
            set
            {
                if (ZoomTime > 0.5f || value)
                    zoomPanel.gameObject.SetActive(value);
            }
        }

        public float ZoomTime { get; private set; }

        public bool IsVisible
        {
            get => _isVisible;
            set
            {
                _isVisible = value;
                if (!_isVisible && zoomPanel != null)
                    zoomPanel.gameObject.SetActive(false);
                if (SelectedCardModel != null)
                    SelectedCardModel.HighlightMode = _isVisible ? HighlightMode.Selected : HighlightMode.Off;
                Redisplay();
            }
        }

        private bool _isVisible;
        public bool WasVisible { get; private set; }

        private static bool IsNameVisible
        {
            get => PlayerPrefs.GetInt(PlayerPrefsIsNameVisible, 0) == 1;
            set => PlayerPrefs.SetInt(PlayerPrefsIsNameVisible, value ? 1 : 0);
        }

        private void OnEnable()
        {
            CardGameManager.Instance.OnSceneActions.Add(ResetInfo);
        }

        private void Start()
        {
            ResetInfo();
        }

        private void Update()
        {
            if (Zoom)
                ZoomTime += Time.deltaTime;
            else
                ZoomTime = 0;
            WasVisible = IsVisible;
            if (!(IsVisible || Zoom) || SelectedCardModel == null || CardGameManager.Instance.ModalCanvas != null)
                return;

            if (nameVisibleButton.gameObject.activeSelf != SelectedCardModel.isFacedown)
                nameVisibleButton.gameObject.SetActive(SelectedCardModel.isFacedown);
            if (nameVisibleButton.gameObject.activeSelf != SelectedCardModel.isFacedown)
                nameVisibleButton.gameObject.SetActive(SelectedCardModel.isFacedown);
            var isNameVisible = IsNameVisible;
            if (nameVisibleButtonImage.gameObject.activeSelf != isNameVisible)
                nameVisibleButtonImage.gameObject.SetActive(isNameVisible);
            if (nameInvisibleButtonImage.gameObject.activeSelf == isNameVisible)
                nameInvisibleButtonImage.gameObject.SetActive(!isNameVisible);
            nameTexts[0].color = isNameVisible || !SelectedCardModel.isFacedown ? Color.white : Color.clear;

            if (EventSystem.current.currentSelectedGameObject == null && !EventSystem.current.alreadySelecting)
                EventSystem.current.SetSelectedGameObject(gameObject);

            if (Inputs.IsPageVertical && Instance.Mode == CardViewerMode.Maximal)
            {
                if (Inputs.IsPageDown && !Inputs.WasPageDown)
                    maximalScrollRect.verticalNormalizedPosition =
                        Mathf.Clamp01(maximalScrollRect.verticalNormalizedPosition + 0.1f);
                else if (Inputs.IsPageUp && !Inputs.WasPageDown)
                    maximalScrollRect.verticalNormalizedPosition =
                        Mathf.Clamp01(maximalScrollRect.verticalNormalizedPosition - 0.1f);
            }

            if (Inputs.IsSubmit)
            {
                if (!Zoom && Mode == CardViewerMode.Maximal)
                    Mode = CardViewerMode.Expanded;
                else
                    SelectedCardModel.DefaultAction?.Invoke(SelectedCardModel);
            }
            else if (Inputs.IsFocusBack && !Inputs.WasFocusBack)
                DecrementProperty();
            else if (Inputs.IsFocusNext && !Inputs.WasFocusNext)
                IncrementProperty();
            else if (Inputs.IsCancel)
            {
                if (!Zoom && Mode == CardViewerMode.Maximal)
                    Mode = CardViewerMode.Expanded;
                SelectedCardModel = null;
            }
        }

        public void Preview(CardModel cardModel)
        {
            preview.alpha = 1;
            previewNameText.text = cardModel.Value.Name;
            previewIdText.text = cardModel.Value.Id;
        }

        private void ResetInfo()
        {
            foreach (var cardAspectRatioFitter in cardAspectRatioFitters)
                cardAspectRatioFitter.aspectRatio = CardGameManager.Current.CardAspectRatio;
            foreach (var text in uniqueIdTexts)
                text.transform.parent.parent.gameObject.SetActive(!CardGameManager.Current.CardNameIsUnique);

            PropertyOptions.Clear();
            PropertyOptions.Add(new Dropdown.OptionData() {text = SetLabel});
            PropertyOptions.Add(new Dropdown.OptionData() {text = IdLabel});
            DisplayNameLookup.Clear();
            foreach (var propertyDef in CardGameManager.Current.CardProperties)
                AddProperty(propertyDef);

            foreach (var propertySelector in propertySelectors)
            {
                propertySelector.options = PropertyOptions;
                propertySelector.value = PrimaryPropertyIndex;
                propertySelector.onValueChanged.Invoke(propertySelector.value);
            }
        }

        private void AddProperty(PropertyDef propertyDef, string parentPrefix = "")
        {
            if (propertyDef == null || parentPrefix == null)
            {
                Debug.LogWarning("AddProperty::NullProperty");
                return;
            }

            if (propertyDef.Type is PropertyType.Object or PropertyType.ObjectList)
            {
                foreach (var childProperty in propertyDef.Properties)
                    AddProperty(childProperty, parentPrefix + propertyDef.Name + PropertyDef.ObjectDelimiter);
            }
            else
            {
                var displayName = !string.IsNullOrEmpty(propertyDef.Display) ? propertyDef.Display : propertyDef.Name;
                PropertyOptions.Add(new Dropdown.OptionData() {text = displayName});
                DisplayNameLookup[displayName] = parentPrefix + propertyDef.Name;
            }
        }

        [UsedImplicitly]
        public void DecrementProperty()
        {
            SelectedPropertyIndex--;
        }

        [UsedImplicitly]
        public void IncrementProperty()
        {
            SelectedPropertyIndex++;
        }

        public void SetImageSprite(Sprite imageSprite)
        {
            foreach (var cardImage in cardImages)
                cardImage.sprite = imageSprite ? imageSprite : CardGameManager.Current.CardBackImageSprite;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EventSystem.current.SetSelectedGameObject(gameObject, eventData);
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!Zoom)
                IsVisible = true;
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!Zoom)
                IsVisible = false;
        }

        public void MaximizeOn(CardModel cardModel)
        {
            SelectedCardModel = cardModel;
            Mode = CardViewerMode.Maximal;
        }

        public void ZoomOn(CardModel cardModel)
        {
            var isVisible = IsVisible;
            if (!EventSystem.current.alreadySelecting)
                EventSystem.current.SetSelectedGameObject(gameObject);
            SelectedCardModel = cardModel;
            IsVisible = isVisible;
            Zoom = true;
        }

        [UsedImplicitly]
        public void SetMode(int mode)
        {
            Mode = (CardViewerMode) mode;
        }

        [UsedImplicitly]
        public void ToggleIsNameVisible()
        {
            IsNameVisible = !IsNameVisible;
        }

        private void Redisplay()
        {
            HidePreview();
            minimal.alpha = IsVisible && Mode == CardViewerMode.Minimal ? 1 : 0;
            minimal.interactable = IsVisible && Mode == CardViewerMode.Minimal;
            minimal.blocksRaycasts = IsVisible && Mode == CardViewerMode.Minimal;
            expanded.alpha = IsVisible && Mode == CardViewerMode.Expanded ? 1 : 0;
            expanded.interactable = IsVisible && Mode == CardViewerMode.Expanded;
            expanded.blocksRaycasts = IsVisible && Mode == CardViewerMode.Expanded;
            maximal.alpha = IsVisible && Mode == CardViewerMode.Maximal ? 1 : 0;
            maximal.interactable = IsVisible && Mode == CardViewerMode.Maximal;
            maximal.blocksRaycasts = IsVisible && Mode == CardViewerMode.Maximal;
        }

        private void ResetTexts()
        {
            foreach (var nameText in nameTexts)
                nameText.text = SelectedCardModel.Value.Name;
            foreach (var uniqueIdText in uniqueIdTexts)
                uniqueIdText.text = SelectedCardModel.Id;
            idText.text = IdLabel + Delimiter + SelectedCardModel.Id;
            setText.text = SetLabel + Delimiter
                                    + (CardGameManager.Current.Sets.TryGetValue(SelectedCardModel.Value.SetCode,
                                        out var currentSet)
                                        ? currentSet.ToString()
                                        : SelectedCardModel.Value.SetCode);
            foreach (var propertyText in PropertyTexts)
                Destroy(propertyText.gameObject);
            PropertyTexts.Clear();
            for (var i = 2; i < PropertyOptions.Count; i++)
            {
                var newPropertyText = Instantiate(propertyTextTemplate.gameObject, maximalScrollRect.content)
                    .GetComponent<Text>();
                newPropertyText.gameObject.SetActive(true);
                newPropertyText.text = PropertyOptions[i].text + Delimiter
                                                               + (DisplayNameLookup.TryGetValue(PropertyOptions[i].text,
                                                                   out var propertyName)
                                                                   ? SelectedCardModel.Value.GetPropertyValueString(
                                                                       propertyName)
                                                                   : string.Empty);
                PropertyTexts.Add(newPropertyText);
            }

            maximalScrollRect.verticalNormalizedPosition = 1;
            ResetPropertyValueText();
        }

        private void ResetPropertyValueText()
        {
            if (SelectedCardModel == null)
            {
                foreach (var propertyValueText in propertyValueTexts)
                    propertyValueText.text = string.Empty;
                return;
            }

            var newContentTextValue = string.Empty;
            if (SelectedPropertyIndex > 1)
                newContentTextValue = SelectedCardModel.Value.GetPropertyValueString(SelectedPropertyName);
            else if (SelectedPropertyIndex == 1)
                newContentTextValue = SelectedCardModel.Id;
            else if (CardGameManager.Current.Sets.TryGetValue(SelectedCardModel.Value.SetCode, out var currentSet))
                newContentTextValue = currentSet.ToString();

            foreach (var propertyValueText in propertyValueTexts)
                propertyValueText.text = newContentTextValue;
        }

        public void HidePreview()
        {
            preview.alpha = 0;
        }
    }
}
