// GENERATED AUTOMATICALLY FROM 'Assets/Core/PlayerControls.inputactions'

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class @PlayerControls : IInputActionCollection, IDisposable
{
    public InputActionAsset asset { get; }
    public @PlayerControls()
    {
        asset = InputActionAsset.FromJson(@"{
    ""name"": ""PlayerControls"",
    ""maps"": [
        {
            ""name"": ""Gameplay"",
            ""id"": ""8b305517-d6d7-4651-8d97-156b4985dd1e"",
            ""actions"": [
                {
                    ""name"": ""Movement"",
                    ""type"": ""Value"",
                    ""id"": ""2eafd46e-10d3-4a4a-a02d-a7150389bf35"",
                    ""expectedControlType"": ""Vector2"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Jump"",
                    ""type"": ""Value"",
                    ""id"": ""9517d14c-3c13-420a-adad-8ab72d527007"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Spindash"",
                    ""type"": ""Value"",
                    ""id"": ""92a0912c-e35b-4e48-8287-e2e511759eee"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Fire"",
                    ""type"": ""Value"",
                    ""id"": ""3753e377-a961-4534-baf8-20ec4f9c1670"",
                    ""expectedControlType"": """",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Zoom"",
                    ""type"": ""Value"",
                    ""id"": ""b7bf93e1-7dd4-492e-a989-822eddf7acd7"",
                    ""expectedControlType"": ""Axis"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Talk"",
                    ""type"": ""Button"",
                    ""id"": ""13d847b7-89fa-43d3-a600-053b69da1bd7"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""View Scores"",
                    ""type"": ""Button"",
                    ""id"": ""ff2a2f38-050a-46b3-98f2-db2987c570a6"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Center Camera"",
                    ""type"": ""Button"",
                    ""id"": ""169b5944-5fa4-4b91-a048-cbf2aee769a1"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Next Weapon"",
                    ""type"": ""Button"",
                    ""id"": ""8cb23682-cd6d-4da6-9ebc-2c1aea6a2121"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Prev Weapon"",
                    ""type"": ""Button"",
                    ""id"": ""92153a79-fb1f-4c47-9fe9-c33692e22861"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                },
                {
                    ""name"": ""Weapon Wheel"",
                    ""type"": ""Button"",
                    ""id"": ""e291a211-2985-4438-ba1d-c8ba04e44cb3"",
                    ""expectedControlType"": ""Button"",
                    ""processors"": """",
                    ""interactions"": """"
                }
            ],
            ""bindings"": [
                {
                    ""name"": ""2D Vector"",
                    ""id"": ""011c8c88-0ffe-4220-a60f-46c044e264dd"",
                    ""path"": ""2DVector"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""up"",
                    ""id"": ""30f97a16-b6a9-4973-a735-7d4a78cdfaed"",
                    ""path"": ""<Keyboard>/w"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""down"",
                    ""id"": ""f1af553a-126d-4baf-a57c-a591f078ab01"",
                    ""path"": ""<Keyboard>/s"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""left"",
                    ""id"": ""dab5ad7e-7c36-44ac-9c53-51083e68da72"",
                    ""path"": ""<Keyboard>/a"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""right"",
                    ""id"": ""b3370fb3-289a-4110-8004-1695c1ccd4b7"",
                    ""path"": ""<Keyboard>/d"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Movement"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""68787d2c-381b-4503-a622-48546286de45"",
                    ""path"": ""<Mouse>/leftButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Fire"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""7e4cf74f-81f2-4ff3-a37d-31770a58d27c"",
                    ""path"": ""<Mouse>/scroll/y"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Zoom"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""1D Axis"",
                    ""id"": ""889a3e11-c76e-4901-8463-cd3296a89fd3"",
                    ""path"": ""1DAxis"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Zoom"",
                    ""isComposite"": true,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": ""negative"",
                    ""id"": ""466e9760-27f8-4e83-902e-475b719d39e1"",
                    ""path"": ""<Keyboard>/leftBracket"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Zoom"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": ""positive"",
                    ""id"": ""e218c489-fad7-4e0c-8b6c-60dd927926d5"",
                    ""path"": ""<Keyboard>/rightBracket"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Zoom"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": true
                },
                {
                    ""name"": """",
                    ""id"": ""b9ad8515-cccd-4595-a6ad-b5b08e8f0615"",
                    ""path"": ""<Keyboard>/t"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Talk"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""ab7dc132-2bf3-4013-b02b-ff6ffa702f38"",
                    ""path"": ""<Mouse>/rightButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Jump"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""d51da3c8-5fe0-44d5-9712-5672416c720a"",
                    ""path"": ""<Keyboard>/space"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Jump"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""d73cab5c-7346-4fa3-bc23-bbda915a54df"",
                    ""path"": ""<Keyboard>/tab"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""View Scores"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""468aa807-787e-484a-be52-b3af96f78153"",
                    ""path"": ""<Keyboard>/f"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Center Camera"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""319be93a-a1bf-4549-8bf8-ae91fb4c84de"",
                    ""path"": ""<Keyboard>/leftShift"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Spindash"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""f0234fd1-b9c0-4edb-9d53-719cc547ffc6"",
                    ""path"": ""<Mouse>/middleButton"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Spindash"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""335a215f-14f4-4374-b983-e2311bee4f52"",
                    ""path"": ""<Keyboard>/e"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Next Weapon"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""1edac87a-5ecc-44b4-b988-efa9675e41cc"",
                    ""path"": ""<Keyboard>/q"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Prev Weapon"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                },
                {
                    ""name"": """",
                    ""id"": ""75883754-9162-4f46-bd75-dffa06aed965"",
                    ""path"": ""<Keyboard>/r"",
                    ""interactions"": """",
                    ""processors"": """",
                    ""groups"": """",
                    ""action"": ""Weapon Wheel"",
                    ""isComposite"": false,
                    ""isPartOfComposite"": false
                }
            ]
        }
    ],
    ""controlSchemes"": []
}");
        // Gameplay
        m_Gameplay = asset.FindActionMap("Gameplay", throwIfNotFound: true);
        m_Gameplay_Movement = m_Gameplay.FindAction("Movement", throwIfNotFound: true);
        m_Gameplay_Jump = m_Gameplay.FindAction("Jump", throwIfNotFound: true);
        m_Gameplay_Spindash = m_Gameplay.FindAction("Spindash", throwIfNotFound: true);
        m_Gameplay_Fire = m_Gameplay.FindAction("Fire", throwIfNotFound: true);
        m_Gameplay_Zoom = m_Gameplay.FindAction("Zoom", throwIfNotFound: true);
        m_Gameplay_Talk = m_Gameplay.FindAction("Talk", throwIfNotFound: true);
        m_Gameplay_ViewScores = m_Gameplay.FindAction("View Scores", throwIfNotFound: true);
        m_Gameplay_CenterCamera = m_Gameplay.FindAction("Center Camera", throwIfNotFound: true);
        m_Gameplay_NextWeapon = m_Gameplay.FindAction("Next Weapon", throwIfNotFound: true);
        m_Gameplay_PrevWeapon = m_Gameplay.FindAction("Prev Weapon", throwIfNotFound: true);
        m_Gameplay_WeaponWheel = m_Gameplay.FindAction("Weapon Wheel", throwIfNotFound: true);
    }

    public void Dispose()
    {
        UnityEngine.Object.Destroy(asset);
    }

    public InputBinding? bindingMask
    {
        get => asset.bindingMask;
        set => asset.bindingMask = value;
    }

    public ReadOnlyArray<InputDevice>? devices
    {
        get => asset.devices;
        set => asset.devices = value;
    }

    public ReadOnlyArray<InputControlScheme> controlSchemes => asset.controlSchemes;

    public bool Contains(InputAction action)
    {
        return asset.Contains(action);
    }

    public IEnumerator<InputAction> GetEnumerator()
    {
        return asset.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Enable()
    {
        asset.Enable();
    }

    public void Disable()
    {
        asset.Disable();
    }

    // Gameplay
    private readonly InputActionMap m_Gameplay;
    private IGameplayActions m_GameplayActionsCallbackInterface;
    private readonly InputAction m_Gameplay_Movement;
    private readonly InputAction m_Gameplay_Jump;
    private readonly InputAction m_Gameplay_Spindash;
    private readonly InputAction m_Gameplay_Fire;
    private readonly InputAction m_Gameplay_Zoom;
    private readonly InputAction m_Gameplay_Talk;
    private readonly InputAction m_Gameplay_ViewScores;
    private readonly InputAction m_Gameplay_CenterCamera;
    private readonly InputAction m_Gameplay_NextWeapon;
    private readonly InputAction m_Gameplay_PrevWeapon;
    private readonly InputAction m_Gameplay_WeaponWheel;
    public struct GameplayActions
    {
        private @PlayerControls m_Wrapper;
        public GameplayActions(@PlayerControls wrapper) { m_Wrapper = wrapper; }
        public InputAction @Movement => m_Wrapper.m_Gameplay_Movement;
        public InputAction @Jump => m_Wrapper.m_Gameplay_Jump;
        public InputAction @Spindash => m_Wrapper.m_Gameplay_Spindash;
        public InputAction @Fire => m_Wrapper.m_Gameplay_Fire;
        public InputAction @Zoom => m_Wrapper.m_Gameplay_Zoom;
        public InputAction @Talk => m_Wrapper.m_Gameplay_Talk;
        public InputAction @ViewScores => m_Wrapper.m_Gameplay_ViewScores;
        public InputAction @CenterCamera => m_Wrapper.m_Gameplay_CenterCamera;
        public InputAction @NextWeapon => m_Wrapper.m_Gameplay_NextWeapon;
        public InputAction @PrevWeapon => m_Wrapper.m_Gameplay_PrevWeapon;
        public InputAction @WeaponWheel => m_Wrapper.m_Gameplay_WeaponWheel;
        public InputActionMap Get() { return m_Wrapper.m_Gameplay; }
        public void Enable() { Get().Enable(); }
        public void Disable() { Get().Disable(); }
        public bool enabled => Get().enabled;
        public static implicit operator InputActionMap(GameplayActions set) { return set.Get(); }
        public void SetCallbacks(IGameplayActions instance)
        {
            if (m_Wrapper.m_GameplayActionsCallbackInterface != null)
            {
                @Movement.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnMovement;
                @Movement.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnMovement;
                @Movement.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnMovement;
                @Jump.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnJump;
                @Jump.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnJump;
                @Jump.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnJump;
                @Spindash.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnSpindash;
                @Spindash.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnSpindash;
                @Spindash.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnSpindash;
                @Fire.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnFire;
                @Fire.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnFire;
                @Fire.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnFire;
                @Zoom.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnZoom;
                @Zoom.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnZoom;
                @Zoom.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnZoom;
                @Talk.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnTalk;
                @Talk.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnTalk;
                @Talk.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnTalk;
                @ViewScores.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnViewScores;
                @ViewScores.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnViewScores;
                @ViewScores.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnViewScores;
                @CenterCamera.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnCenterCamera;
                @CenterCamera.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnCenterCamera;
                @CenterCamera.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnCenterCamera;
                @NextWeapon.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnNextWeapon;
                @NextWeapon.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnNextWeapon;
                @NextWeapon.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnNextWeapon;
                @PrevWeapon.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnPrevWeapon;
                @PrevWeapon.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnPrevWeapon;
                @PrevWeapon.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnPrevWeapon;
                @WeaponWheel.started -= m_Wrapper.m_GameplayActionsCallbackInterface.OnWeaponWheel;
                @WeaponWheel.performed -= m_Wrapper.m_GameplayActionsCallbackInterface.OnWeaponWheel;
                @WeaponWheel.canceled -= m_Wrapper.m_GameplayActionsCallbackInterface.OnWeaponWheel;
            }
            m_Wrapper.m_GameplayActionsCallbackInterface = instance;
            if (instance != null)
            {
                @Movement.started += instance.OnMovement;
                @Movement.performed += instance.OnMovement;
                @Movement.canceled += instance.OnMovement;
                @Jump.started += instance.OnJump;
                @Jump.performed += instance.OnJump;
                @Jump.canceled += instance.OnJump;
                @Spindash.started += instance.OnSpindash;
                @Spindash.performed += instance.OnSpindash;
                @Spindash.canceled += instance.OnSpindash;
                @Fire.started += instance.OnFire;
                @Fire.performed += instance.OnFire;
                @Fire.canceled += instance.OnFire;
                @Zoom.started += instance.OnZoom;
                @Zoom.performed += instance.OnZoom;
                @Zoom.canceled += instance.OnZoom;
                @Talk.started += instance.OnTalk;
                @Talk.performed += instance.OnTalk;
                @Talk.canceled += instance.OnTalk;
                @ViewScores.started += instance.OnViewScores;
                @ViewScores.performed += instance.OnViewScores;
                @ViewScores.canceled += instance.OnViewScores;
                @CenterCamera.started += instance.OnCenterCamera;
                @CenterCamera.performed += instance.OnCenterCamera;
                @CenterCamera.canceled += instance.OnCenterCamera;
                @NextWeapon.started += instance.OnNextWeapon;
                @NextWeapon.performed += instance.OnNextWeapon;
                @NextWeapon.canceled += instance.OnNextWeapon;
                @PrevWeapon.started += instance.OnPrevWeapon;
                @PrevWeapon.performed += instance.OnPrevWeapon;
                @PrevWeapon.canceled += instance.OnPrevWeapon;
                @WeaponWheel.started += instance.OnWeaponWheel;
                @WeaponWheel.performed += instance.OnWeaponWheel;
                @WeaponWheel.canceled += instance.OnWeaponWheel;
            }
        }
    }
    public GameplayActions @Gameplay => new GameplayActions(this);
    public interface IGameplayActions
    {
        void OnMovement(InputAction.CallbackContext context);
        void OnJump(InputAction.CallbackContext context);
        void OnSpindash(InputAction.CallbackContext context);
        void OnFire(InputAction.CallbackContext context);
        void OnZoom(InputAction.CallbackContext context);
        void OnTalk(InputAction.CallbackContext context);
        void OnViewScores(InputAction.CallbackContext context);
        void OnCenterCamera(InputAction.CallbackContext context);
        void OnNextWeapon(InputAction.CallbackContext context);
        void OnPrevWeapon(InputAction.CallbackContext context);
        void OnWeaponWheel(InputAction.CallbackContext context);
    }
}
