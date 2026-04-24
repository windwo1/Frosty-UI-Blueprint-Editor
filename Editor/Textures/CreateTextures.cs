using Frosty.Core;
using FrostySdk.Ebx;
using FrostySdk.IO;
using FrostySdk.Resources;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media.Imaging;

namespace UIBlueprintEditor.Editor.Textures
{
    public class CreateTextures
    {
        private static Dictionary<dynamic, dynamic> _mappingIdToMapping = UIEditor.MappingIdToMapping;
        private static Dictionary<dynamic, dynamic> _mappingMinValue = UIEditor.MappingMinValue;
        private static Dictionary<dynamic, dynamic> _mappingMaxValue = UIEditor.MappingMaxValue;
        private static Dictionary<dynamic, BitmapImage> _mappingTexture = UIEditor.MappingTexture;

        public static void GetTextures(dynamic rootObject, string textureId)
        {
            foreach (var textureItem in rootObject.Object.Internal.TextureMappings)
            {
                var textureMapGuid = ((PointerRef)textureItem).External.FileGuid;
                var textureMapEbx = App.AssetManager.GetEbxEntry(textureMapGuid);

                EbxAsset textureMapAsset = App.AssetManager.GetEbx(textureMapEbx);
                dynamic rootObjectTextureMap = textureMapAsset.RootObject;

                foreach (dynamic outputEntry in rootObjectTextureMap.Output)
                {
                    if (outputEntry.Id == textureId && !_mappingIdToMapping.ContainsKey(outputEntry.Id))
                    {
                        var min = outputEntry.Min;
                        var max = outputEntry.Max;
                        var textureRef = outputEntry.Texture;

                        var textureGuid = ((PointerRef)textureRef).External.FileGuid;
                        var textureEbx = App.AssetManager.GetEbxEntry(textureGuid);

                        var textureAsset = App.AssetManager.GetEbx(textureEbx);
                        dynamic rootObjectTexture = textureAsset.RootObject;
                        ulong textureRes = rootObjectTexture.Resource;

                        // texture section by NM, modified a little bit to write textures to memory

                        Texture texture = App.AssetManager.GetResAs<Texture>(App.AssetManager.GetResEntry(textureRes));

                        _mappingIdToMapping.Add(outputEntry.Id, outputEntry);
                        _mappingMinValue.Add(outputEntry.Id, min);
                        _mappingMaxValue.Add(outputEntry.Id, max);

                        byte[] textureBytes = TextureExporterToMemory.Export(texture);

                        BitmapImage bitmap = CreateBitmap(textureBytes);

                        _mappingTexture.Add(outputEntry.Id, bitmap);
                    }
                }
            }
        }

        public static BitmapImage CreateBitmap(byte[] textureBytes)
        {
            var bitmap = new BitmapImage();

            using (var stream = new MemoryStream(textureBytes))
            {
                bitmap.BeginInit();

                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = stream;

                bitmap.EndInit();
            }

            return bitmap;
        }
    }
}
