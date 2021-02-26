using UdonSharp;
using UdonToolkit;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UdonSharpEditor;
using UnityEditor;
#endif

namespace EsnyaFactory.UdonDoor
{
    [RequireComponent(typeof(VRC.SDK3.Components.VRCPickup))][CustomName("Door Controller")]
    public class DoorController : UdonSharpBehaviour
    {
        #region Core
        bool isSleeping = true;

        void Start()
        {
            Grabber_Start();
            Door_Start();
            SoundEffect_Start();
        }

        void Update()
        {
            if (isSleeping) return;


            if (Networking.IsOwner(gameObject)) {
                Door_OwnerUpdate();
                SoundEffect_OwnerUpdate();
            }

            Door_PostUpdate();
        }

        override public void OnPickup()
        {
            isSleeping = false;
            Grabber_OnPickup();
        }

        override public void OnDrop()
        {
            Grabber_OnDrop();
        }

        public override void OnDeserialization()
        {
            Door_OnDeserialization();
        }
        #endregion

        #region Grabber


        #endregion

        #region Grabber

        bool isGrabbed = false;
        Vector3 grabberHomePosition;
        Quaternion grabberHomeRotation;

        void Grabber_Start()
        {
            grabberHomePosition = transform.localPosition;
            grabberHomeRotation = transform.localRotation;
        }

        void Grabber_OnPickup()
        {
            isGrabbed = true;
        }
        void Grabber_OnDrop()
        {
            isGrabbed = false;

            transform.localPosition = grabberHomePosition;
            transform.localRotation = grabberHomeRotation;
        }
        #endregion

        #region Door
        [Space][SectionHeader("Door")][UTEditor]
        public Transform door;
        public Vector3 doorLocalAxis = Vector3.up;
        public Vector3 doorLocalSecondaryAxis = Vector3.right;
        [Range(-180, 0)] public float doorMinAngle = 0;
        [Range(0, 180)] public float doorMaxAngle = 90;
        public float doorSpring = 10;
        public float doorDrag = 1;
        Matrix4x4 doorLocalToWorld;
        Matrix4x4 doorWorldToLocal;
        Vector3 doorWorldAxis;
        Vector3 doorWorldSecondaryAxis;
        float prevDoorAngle = 0;
        [UdonSynced(UdonSyncMode.Smooth)] float doorAngle = 0;
        float doorSpeed = 0;

        void Door_Start()
        {
            doorLocalAxis.Normalize();

            doorLocalToWorld = door.localToWorldMatrix;
            doorWorldToLocal = door.worldToLocalMatrix;
            doorWorldAxis = door.TransformDirection(doorLocalAxis);
            doorWorldSecondaryAxis = door.TransformDirection(doorLocalSecondaryAxis);
        }

        void Door_OwnerUpdate()
        {
            if (!Networking.IsOwner(gameObject)) return;

            if (isGrabbed) {
                var doorToGrabberWorldVector = transform.position - door.position;
                var doorToGrabberOnDoorAxisWorldDirection = doorToGrabberWorldVector - doorWorldAxis * Vector3.Dot(doorToGrabberWorldVector, doorWorldAxis);
                var doorToGrabberOnDoorAxisLocalDirection = doorWorldToLocal.MultiplyVector(doorToGrabberOnDoorAxisWorldDirection);
                var targetDoorAngle = Vector3.SignedAngle(doorLocalSecondaryAxis, doorToGrabberOnDoorAxisLocalDirection, doorLocalAxis);

                doorSpeed = (targetDoorAngle - doorAngle) * doorSpring;
            // } else if (prevDoorAngle == doorAngle) {
            //     doorSpeed = 0;
            //     isSleeping = true;
            } else  {
                doorSpeed = doorSpeed * Mathf.Max(1 - doorDrag * Time.deltaTime, 0);
            }

            doorAngle = Mathf.Clamp(doorAngle + doorSpeed * Time.deltaTime, doorMinAngle, doorMaxAngle);

            door.localRotation = Quaternion.AngleAxis(doorAngle, doorLocalAxis);
        }
        void Door_PostUpdate()
        {
            prevDoorAngle = doorAngle;
        }

        void Door_OnDeserialization()
        {
            door.localRotation = Quaternion.AngleAxis(Mathf.Clamp(doorAngle, doorMinAngle, doorMaxAngle), doorLocalAxis);
        }
        #endregion

        #region Sound Effect
        [Space]
        [SectionHeader("Sound Effect")][UTEditor]
        public AudioSource audioSource;
        public AudioClip creakSound;
        public AudioClip closedSound;
        [Range(0, 90)] public float creakThreshold = 10;
        bool creakPlayed = false;

        void SoundEffect_Start()
        {
            audioSource.transform.SetParent(door);
        }

        void SoundEffect_OwnerUpdate()
        {
            if (isGrabbed) {
                if (!creakPlayed && Mathf.Abs(doorSpeed) > creakThreshold) {
                    creakPlayed = true;
                    SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayCreak));
                }
            } else {
                creakPlayed = false;
            }

            if (prevDoorAngle != 0 && doorAngle == 0) {
                SendCustomNetworkEvent(NetworkEventTarget.All, nameof(PlayClosed));
            }
        }

        void PlayOneShot(AudioClip clip)
        {
            if (clip == null) return;
            audioSource.PlayOneShot(clip);
        }

        public void PlayCreak()
        {
            PlayOneShot(creakSound);
        }

        public void PlayClosed()
        {
            PlayOneShot(closedSound);
        }
        #endregion

        #region Editor
#if UNITY_EDITOR && !COMPILER_UDONSHARP
/*
        void OnDrawGizmos() {
            if (doorWorldAxis == Vector3.zero) Start();

            Gizmos.color = Color.green;
            Gizmos.DrawRay(door.position, doorWorldAxis);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(door.position, Quaternion.AngleAxis(doorAngle, doorWorldAxis) * doorWorldSecondaryAxis);
        }
        */
#endif
        #endregion
    }
}
