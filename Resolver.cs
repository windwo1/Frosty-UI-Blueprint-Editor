using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Managers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UIBlueprintEditor
{
    public static class Resolver
    {
        public static object Resolve(this PointerRef pointerRef)
        {
            object pointerRefValue = null;

            if (pointerRef.Type == PointerRefType.External)
            {
                EbxImportReference importReference = pointerRef.External;

                EbxAssetEntry importEntry = App.AssetManager.GetEbxEntry(importReference.FileGuid);
                EbxAsset importAsset = App.AssetManager.GetEbx(importEntry);

                pointerRefValue = importAsset.GetObject(importReference.ClassGuid);
            }
            else if (pointerRef.Type == PointerRefType.Internal)
            {
                pointerRefValue = pointerRef.Internal;
            }

            // If it isn't either of these, it is a null pointerref, so nothing has to be set
            return pointerRefValue;
        }
    }
}
