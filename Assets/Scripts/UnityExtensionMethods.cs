﻿using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;
using Object = UnityEngine.Object;

public static class ThreadSafeRandom
{
    [ThreadStatic] private static System.Random _local;
    public static System.Random ThisThreadsRandom => _local ?? (_local = new System.Random(unchecked(Environment.TickCount * 31 + System.Threading.Thread.CurrentThread.ManagedThreadId)));
}

static public class UnityExtensionMethods
{
    public const string AndroidStreamingAssetsDirectory = "assets/";
    public const string AndroidStreamingAssetsInternalDataDirectory = "assets/bin/";
    public const string MetaExtension = ".meta";
    public const string ZipExtension = ".zip";

    static public void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1) {
            n--;
            int k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
            T value = list [k];
            list [k] = list [n];
            list [n] = value;
        }
    }

    static public T FindInParents<T>(this GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component != null)
            return component;

        Transform transform = go.transform.parent;
        while (transform != null && component == null) {
            component = transform.gameObject.GetComponent<T>();
            transform = transform.parent;
        }
        return component;
    }

    static public T GetOrAddComponent<T>(this GameObject go) where T: Component
    {
        return go.GetComponent<T>() ?? go.AddComponent<T>();
    }

    static public void DestroyAllChildren(this Transform parent)
    {
        for (int i = parent.transform.childCount - 1; i >= 0; i--) {
            Transform child = parent.GetChild(i);
            child.SetParent(null);
            Object.Destroy(child.gameObject);
        }
    }

    public static Vector2 CalculateMean(List<Vector2> list)
    {
        if (list == null)
            return Vector2.zero;
        return list.Aggregate(Vector2.zero, (current, vector) => current + vector) / list.Count;
    }

    public static string GetSafeFilePath(string filePath)
    {
        return !string.IsNullOrEmpty(filePath) ? string.Join("_", filePath.Split(Path.GetInvalidPathChars())) : string.Empty;
    }

    public static string GetSafeFileName(string fileName)
    {
        return !string.IsNullOrEmpty(fileName) ? string.Join("_", fileName.Split(Path.GetInvalidFileNameChars())) : string.Empty;
    }

    public static void CopyDirectory(string sourceDir, string targetDir)
    {
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        foreach (string filePath in Directory.GetFiles(sourceDir))
            if (!filePath.EndsWith(MetaExtension))
                File.Copy(filePath, Path.Combine(targetDir, Path.GetFileName(filePath)));

        foreach (string directory in Directory.GetDirectories(sourceDir))
            if (!string.IsNullOrEmpty(directory))
                CopyDirectory(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
    }

    public static void ExtractAndroidStreamingAssets(string targetPath)
    {
        if (!Directory.Exists(targetPath))
            Directory.CreateDirectory(targetPath);

        if (targetPath[targetPath.Length - 1] != '/' || targetPath[targetPath.Length - 1] != '\\')
            targetPath += '/';

        HashSet<string> createdDirectories = new HashSet<string>();

        ZipFile zf = null;
        try {
            using (FileStream fs = File.OpenRead(Application.dataPath)) {
                zf = new ZipFile(fs);
                foreach (ZipEntry zipEntry in zf) {
                    if (!zipEntry.IsFile)
                        continue;

                    string name = zipEntry.Name;
                    if (!name.StartsWith(AndroidStreamingAssetsDirectory) || name.EndsWith(MetaExtension) ||
                        name.StartsWith(AndroidStreamingAssetsInternalDataDirectory)) continue;

                    name = name.Replace(AndroidStreamingAssetsDirectory, string.Empty);
                    string relativeDir = Path.GetDirectoryName(name);
                    if (!createdDirectories.Contains(relativeDir)) {
                        Directory.CreateDirectory(targetPath + relativeDir);
                        createdDirectories.Add(relativeDir);
                    }

                    byte[] buffer = new byte[4096];
                    using (Stream zipStream = zf.GetInputStream(zipEntry))
                    using (FileStream streamWriter = File.Create(targetPath + name)) {
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    }
                }
            }
        }
        finally {
            if (zf != null) {
                zf.IsStreamOwner = true;
                zf.Close();
            }
        }
    }

    public static void ExtractZip(string zipPath, string targetDir)
    {
        if (!Directory.Exists(targetDir))
            Directory.CreateDirectory(targetDir);

        FastZip fastZip = new FastZip();
        fastZip.ExtractZip(zipPath, targetDir, null);
    }

    public static IEnumerator SaveUrlToFile(string url, string filePath)
    {
        WWW loader = new WWW(url);
        yield return loader;
        if (!string.IsNullOrEmpty(loader.error)) {
            Debug.LogWarning("Failed to load from " + url + ", error: " + loader.error);
            yield break;
        }

        string directory = Path.GetDirectoryName(filePath);
        string fileName = Path.GetFileName(filePath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName)) {
            Debug.LogWarning("Could not save to " + filePath + ", as it is an improperly formed path");
            yield break;
        }

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllBytes(directory + "/" + fileName, loader.bytes);
    }

    public static IEnumerator RunOutputCoroutine<T>(IEnumerator coroutine, Action<T> output) where T : class
    {
        if (coroutine == null || output == null)
            yield break;

        object result = null;
        while (coroutine.MoveNext()) {
            result = coroutine.Current;
            yield return result;
        }
        output(result as T);
    }

    public static IEnumerator CreateAndOutputSpriteFromImageFile(string imageFilePath, string backUpImageUrl)
    {
        if (!File.Exists(imageFilePath))
            yield return SaveUrlToFile(backUpImageUrl, imageFilePath);

        yield return CreateSprite(imageFilePath);
    }
    
    public static Sprite CreateSprite(string textureFilePath)
    {
        if (!File.Exists(textureFilePath)
            return null;
        byte[] fileData = File.ReadAllBytes(filePath);
        Texture2D newTexture = new Texture2D(2, 2);
        newTexture.LoadImage(fileData);
        yield return Sprite.Create(newTexture, new Rect(0, 0, newTexture.width, newTexture.height), new Vector2(0.5f, 0.5f));
    }
}
