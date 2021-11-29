#!/bin/sh

echo ""
echo "#################################"
echo "#   Generating Depot Manifests  #"
echo "#################################"
echo ""

i=1;
export DEPOTS="\n  "
until [ $i -gt 9 ]; do
  eval "currentDepotPath=\$depot${i}Path"
  if [ -n "$currentDepotPath" ]; then
    currentDepot=$((appId+i))
    echo ""
    echo "Adding depot${currentDepot}.vdf ..."
    echo ""
    export DEPOTS="$DEPOTS  \"$currentDepot\" \"depot${currentDepot}.vdf\"\n  "
    cat << EOF > "depot${currentDepot}.vdf"
"DepotBuildConfig"
{
  "DepotID" "$currentDepot"
  "ContentRoot" "$(pwd)/$rootPath"
  "FileMapping"
  {
    "LocalPath" "$currentDepotPath"
    "DepotPath" "."
    "recursive" "1"
  }
  "FileExclusion" "*.pdb"
}
EOF

  cat depot${currentDepot}.vdf
  echo ""
  fi;

  i=$((i+1))
done

echo ""
echo "#################################"
echo "#    Generating App Manifest    #"
echo "#################################"
echo ""

mkdir BuildOutput

cat << EOF > "manifest.vdf"
"appbuild"
{
  "appid" "$appId"
  "desc" "$buildDescription"
  "buildoutput" "BuildOutput"
  "contentroot" "$(pwd)"
  "setlive" "$releaseBranch"

  "depots"
  {$(echo "$DEPOTS" | sed 's/\\n/\
/g')}
}
EOF

cat manifest.vdf
echo ""

echo ""
echo "#################################"
echo "#    Copying SteamGuard Files   #"
echo "#################################"
echo ""

mkdir -p /home/runner/Steam/config

if [ -n "$configVdf" ]; then
  echo "Copying /home/runner/Steam/config/config.vdf..."
  echo "$configVdf" > /home/runner/Steam/config/config.vdf
  chmod 777 /home/runner/Steam/config/config.vdf
  cat /home/runner/Steam/config/config.vdf
fi;

if [ -n "$ssfnFileName" ]; then
  echo "Copying /home/runner/Steam/ssfn..."
  export SSFN_DECODED="$(echo $ssfnFileContents | base64 -d)"
  echo "$SSFN_DECODED"
  echo "$ssfnFileContents" | base64 -d > "/home/runner/Steam/$ssfnFileName"
  chmod 777 "/home/runner/Steam/$ssfnFileName"
  echo "/home/runner/Steam/$ssfnFileName"
  cat "/home/runner/Steam/$ssfnFileName"
fi;

echo "Finished Copying SteamGuard Files!"
echo ""

echo ""
echo "#################################"
echo "#        Uploading build        #"
echo "#################################"
echo ""

echo "$steamExecutable"
echo ""
steamcmd +login "$username" "$password" "$mfaCode" +run_app_build $(pwd)/manifest.vdf +quit || (
    echo ""
    echo "#################################"
    echo "#             Errors            #"
    echo "#################################"
    echo ""
    echo "Listing current folder, rootpath, and STEAM_HOME"
    echo ""
    ls -alh
    echo ""
    ls -alh $rootPath
    echo ""
    ls -alh /home/runner/Steam
    echo ""
    echo "Listing logs folder:"
    echo ""
    ls -Ralph /home/runner/Steam/logs/
    echo ""
    echo "Displaying error log"
    echo ""
    cat /home/runner/Steam/logs/stderr.txt
    echo ""
    echo "Displaying bootstrapper log"
    echo ""
    cat /home/runner/Steam/logs/bootstrap_log.txt
    echo ""
    echo "#################################"
    echo "#             Output            #"
    echo "#################################"
    echo ""
    ls -Ralph BuildOutput
    exit 1
  )
