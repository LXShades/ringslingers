using UnityEngine;

namespace Ringslingers.Tests
{
    public class FreeCam : MonoBehaviour
    {
        private float horizontalAim;
        private float verticalAim;

        public Vector3 forward
        {
            get
            {
                float horizontalRads = horizontalAim * Mathf.Deg2Rad, verticalRads = verticalAim * Mathf.Deg2Rad;
                return new Vector3(Mathf.Sin(horizontalRads) * Mathf.Cos(verticalRads), -Mathf.Sin(verticalRads), Mathf.Cos(horizontalRads) * Mathf.Cos(verticalRads));
            }
            set
            {
                horizontalAim = Mathf.Atan2(value.x, value.z) * Mathf.Rad2Deg;
                verticalAim = -Mathf.Asin(value.y) * Mathf.Rad2Deg;
            }
        }
        public Vector3 up { get; set; } = Vector3.up;


        // Update is called once per frame
        void LateUpdate()
        {
            horizontalAim += Input.GetAxisRaw("Mouse X") / 10f;
            verticalAim -= Input.GetAxisRaw("Mouse Y") / 10f;

            horizontalAim = horizontalAim % 360f;
            verticalAim = Mathf.Clamp(verticalAim, -89.99f, 89.99f);

            transform.rotation = Quaternion.LookRotation(forward, up);
        }
    }
}
