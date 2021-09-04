using UnityEngine;

namespace Ringslingers.Tests
{
    public class MovingObject : MonoBehaviour
    {
        public Vector3 moveAxisAndMagnitude;
        public float frequency;

        private Vector3 initialPosition;

        void Awake()
        {
            initialPosition = transform.position;
        }

        // Update is called once per frame
        void Update()
        {
            transform.position = initialPosition + moveAxisAndMagnitude * Mathf.Sin(Time.time * frequency * Mathf.PI * 2f);
        }
    }
}
