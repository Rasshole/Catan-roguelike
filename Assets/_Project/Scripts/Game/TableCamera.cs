using UnityEngine;

namespace CatanRoguelike.Game
{
    public sealed class TableCamera : MonoBehaviour
    {
        [SerializeField] private Transform lookTarget;
        [SerializeField] private float distance = 8f;
        [SerializeField] private float height = 7f;
        [SerializeField] private float orbitSpeed = 40f;

        private float _angle;

        private void Start()
        {
            if (lookTarget == null)
            {
                var target = new GameObject("BoardCenter");
                lookTarget = target.transform;
            }

            transform.position = lookTarget.position + new Vector3(0f, height, -distance);
            transform.LookAt(lookTarget);
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.Q))
                _angle -= orbitSpeed * Time.deltaTime;
            if (Input.GetKey(KeyCode.E))
                _angle += orbitSpeed * Time.deltaTime;

            var offset = Quaternion.Euler(55f, _angle, 0f) * new Vector3(0f, 0f, -distance);
            transform.position = lookTarget.position + offset + Vector3.up * (height * 0.3f);
            transform.LookAt(lookTarget.position + Vector3.up * 0.5f);
        }
    }
}
