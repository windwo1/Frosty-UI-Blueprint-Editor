using Frosty.Core;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UIBlueprintEditor.Editor.Misc
{
    // sometimes the root object is needed, this just makes it easier
    public class CurrentRootObject
    {
        public static dynamic Get()
        {
            EbxAssetEntry openedAsset = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;

            EbxAsset asset = App.AssetManager.GetEbx(openedAsset);
            dynamic rootObject = asset.RootObject;

            return rootObject;
        }
    }
}
