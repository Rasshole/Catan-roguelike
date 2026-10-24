using System.Text;
using CatanRoguelike.Core.Hex;
using UnityEngine;

namespace CatanRoguelike.Game
{
    /// <summary>PlayMode/capture logging for stray robber visuals vs the RobberPawn hierarchy.</summary>
    public static class ColonistRobberCaptureDiagnostics
    {
        public static void LogRobberHexSceneRenderers(BoardView boardView)
        {
            if (boardView == null)
                return;

            HexTileView robberTile = null;
            foreach (var tile in Object.FindObjectsByType<HexTileView>(FindObjectsSortMode.None))
            {
                if (tile.Data.HasRobber)
                {
                    robberTile = tile;
                    break;
                }
            }

            if (robberTile == null)
            {
                Debug.Log("ColonistRobberCaptureDiagnostics: no tile with HasRobber.");
                return;
            }

            var center = robberTile.transform.position;
            float radius = boardView.HexScale * 0.85f;
            var sb = new StringBuilder();
            sb.AppendLine(
                $"ColonistRobberCaptureDiagnostics robberHex={robberTile.Data.Coord} " +
                $"pawnBounds={robberTile.RobberWorldBounds.size} pawnPos={robberTile.RobberWorldPosition}");

            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer == null || !renderer.enabled)
                    continue;

                var t = renderer.transform;
                Vector3 p = t.position;
                float dist = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(center.x, center.z));
                if (dist > radius)
                    continue;

                string path = BuildPath(t);
                bool nameHit = ContainsRobberKeyword(path);
                if (!nameHit && dist > radius * 0.55f)
                    continue;

                sb.AppendLine(
                    $"  renderer path={path} distXZ={dist:F3} bounds={renderer.bounds.size} " +
                    $"enabled={renderer.enabled} mat={renderer.sharedMaterial?.name}");
            }

            Debug.Log(sb.ToString());
        }

        private static bool ContainsRobberKeyword(string path)
        {
            return path.Contains("Robber")
                || path.Contains("robber")
                || path.Contains("Thief")
                || path.Contains("thief")
                || path.Contains("Marker");
        }

        private static string BuildPath(Transform t)
        {
            var parts = new System.Collections.Generic.List<string>(8);
            while (t != null)
            {
                parts.Add(t.name);
                t = t.parent;
            }

            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
