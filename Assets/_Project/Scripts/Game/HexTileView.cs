using CatanRoguelike.Core.Map;
using UnityEngine;

namespace CatanRoguelike.Game
{
    public sealed class HexTileView : MonoBehaviour
    {
        public HexTileData Data { get; private set; }
        private Renderer _renderer;
        private GameObject _robberMarker;

        public void Initialize(HexTileData data, Renderer renderer)
        {
            Data = data;
            _renderer = renderer;
        }

        public void Refresh(HexTileData data)
        {
            Data = data;
            if (_robberMarker != null)
                _robberMarker.SetActive(data.HasRobber);
        }

        public void SetRobberVisible(bool visible)
        {
            if (_robberMarker == null)
            {
                _robberMarker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                _robberMarker.name = "Robber";
                _robberMarker.transform.SetParent(transform, false);
                _robberMarker.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                _robberMarker.transform.localScale = Vector3.one * 0.35f;
                _robberMarker.GetComponent<Renderer>().material.color = Color.black;
            }
            _robberMarker.SetActive(visible);
        }
    }
}
