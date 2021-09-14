using UnityEngine;

namespace Ringslingers.Tests
{
    public class FreeCam : MonoBehaviour
    {
        private float horAngle;
        private float verAngle;

        // Update is called once per frame
        void Update()
        {
            horAngle += Input.GetAxisRaw("Mouse X") / 10f;
            verAngle -= Input.GetAxisRaw("Mouse Y") / 10f;

            horAngle = horAngle % 360f;
            verAngle = Mathf.Clamp(verAngle, -89.99f, 89.99f);

            transform.rotation = Quaternion.Euler(verAngle, horAngle, 0f);
        }
    }
}
