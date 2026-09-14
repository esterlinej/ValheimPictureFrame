using BepInEx;
using Jotunn;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using ValheimPictureFrame.Utils;

namespace ValheimPictureFrame
{
    [BepInPlugin("dfirst.ValheimPictureFrame", "Valheim Picture Frame", "1.4.2")]
    [BepInDependency(Main.ModGuid)]
    public class ValheimPictureFrame : BaseUnityPlugin
    {
        private static readonly string TOKEN_DESC = "$piece_dfirst_pictureframe_description";

        private void Awake()
        {
            AddPiece();
        }

        private void AddPiece()
        {
            var assetBundle = AssetBundleHelper.GetAssetBundleFromResources("pictureframe");

            string[] pictureFrames =
            {
                "PictureFrame",
                "PictureFrameVertical",
                "PictureFrameSquare",
            };

            PieceConfig config = new PieceConfig()
            {
                PieceTable = "Hammer",
                Description = TOKEN_DESC,
                Requirements = new[]
    {
                    new RequirementConfig()
                    {
                        Item = "FineWood",
                        Amount = 6,
                        Recover = true
                    },
                    new RequirementConfig()
                    {
                        Item = "BronzeNails",
                        Amount = 2,
                        Recover = true
                    }
                }
            };

            foreach (var prefabName in pictureFrames)
            {
                var prefab = assetBundle.LoadAsset<GameObject>($"Assets/Pieces/PictureFrame/{prefabName}.prefab");
                switch (prefabName)
                {
                    case "PictureFrame":
                        prefab.AddComponent<PictureFrame>();
                        break;
                    case "PictureFrameVertical":
                        prefab.AddComponent<PictureFrameVertical>();
                        break;
                    case "PictureFrameSquare":
                        prefab.AddComponent<PictureFrameSquare>();
                        break;
                    default:
                        break;
                }

                var piece = new CustomPiece(prefab, false, config);
                PieceManager.Instance.AddPiece(piece);
            }

            assetBundle.Unload(false);
        }
    }
}