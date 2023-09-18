using Mirror;
using UnityEngine;

public class PlayerSounds : NetworkBehaviour
{
    public enum PlayerSoundType
    {
        Jump,
        Thok,
        RingDrop,
        ShieldGain,
        ShieldLoss,
        SpinRoll,
        SpinCharge,
        SpinRelease,
        NumLimit = 8
    }

    [Header("Sounds")]
    public GameSound jumpSound = new GameSound();
    public GameSound thokSound = new GameSound();
    public GameSound ringDropSound = new GameSound();
    public GameSound shieldGainSound = new GameSound();
    public GameSound shieldLossSound = new GameSound();
    public GameSound spinRollSound = new GameSound();
    public GameSound spinChargeSound = new GameSound();
    public GameSound spinReleaseSound = new GameSound();

    // The below are only played locally and don't need to be networked
    public GameSound splashSound = new GameSound();
    public GameSound flySound = new GameSound();
    public int flySoundPerSecond = 6;

    // current layout: NNsssSSS where N = number (looping) 
    const int kNumCountBits = 2;
    const int kCountMask = (~0 << kNumSoundBitsTotal);
    const int kSoundMask = (int)(PlayerSoundType.NumLimit - 1);
    const int kNumSoundBitsTotal = 6;
    const int kNumSoundBitsSingle = 3;

    public byte soundHistory { get; private set; }

    private byte lastReceivedSoundHistory;

    private bool wasInWater = false;

    private PlayerCharacterMovement movement;

    private void Awake()
    {
        movement = GetComponent<PlayerCharacterMovement>();
    }

    private void LateUpdate()
    {
        // We can do some sound effects locally at the end of all ticks/reconciliations etc
        if (movement.isInWater != wasInWater)
            GameSounds.PlaySound(gameObject, splashSound);

        wasInWater = movement.isInWater;
    }

    public void PlayNetworked(PlayerSoundType sound)
    {
        if (NetworkServer.active)
            PushSoundToHistory(sound);

        PlayLocally(sound);
    }

    public void PlayLocally(PlayerSoundType sound, bool wasReceivedFromServer = false)
    {
        if (hasAuthority && wasReceivedFromServer && sound != PlayerSoundType.RingDrop)
            return; // the rest of our sounds we predict locally. more server-triggered ones might arise in the future
        if (wasReceivedFromServer && NetworkServer.active)
            return; // nah, we don't need to do this

        switch (sound)
        {
            case PlayerSoundType.Jump:
                GameSounds.PlaySound(gameObject, jumpSound);
                break;
            case PlayerSoundType.Thok:
                GameSounds.PlaySound(gameObject, thokSound);
                break;
            case PlayerSoundType.RingDrop:
                GameSounds.PlaySound(gameObject, ringDropSound);
                break;
            case PlayerSoundType.ShieldGain:
                GameSounds.PlaySound(gameObject, shieldGainSound);
                break;
            case PlayerSoundType.ShieldLoss:
                GameSounds.PlaySound(gameObject, shieldLossSound);
                break;
            case PlayerSoundType.SpinRoll:
                GameSounds.PlaySound(gameObject, spinRollSound);
                break;
            case PlayerSoundType.SpinCharge:
                GameSounds.PlaySound(gameObject, spinChargeSound);
                break;
            case PlayerSoundType.SpinRelease:
                GameSounds.PlaySound(gameObject, spinReleaseSound);
                break;
        }
    }

    public void PushSoundToHistory(PlayerSoundType sound)
    {
        int count = ((soundHistory >> kNumSoundBitsTotal) + 1) & ((1 << kNumCountBits) - 1);

        soundHistory <<= kNumSoundBitsSingle;
        soundHistory |= (byte)sound;
        soundHistory &= ~kCountMask;
        soundHistory |= (byte)(count << kNumSoundBitsTotal);
    }

    public void ReceiveSoundHistory(byte soundHistory)
    {
        int num = soundHistory >> kNumSoundBitsTotal;
        int lastNum = lastReceivedSoundHistory >> kNumSoundBitsTotal;
        int firstPrevious = (soundHistory & 0b111);
        int secondPrevious = (soundHistory & 0b111) >> kNumSoundBitsSingle;

        int diff = num - lastNum;
        if (diff < 0)
            diff += (1 << kNumCountBits);

        if (diff >= 1)
            PlayLocally((PlayerSoundType)firstPrevious, true);
        if (diff >= 2)
            PlayLocally((PlayerSoundType)secondPrevious, true);

        lastReceivedSoundHistory = soundHistory;
    }
}
