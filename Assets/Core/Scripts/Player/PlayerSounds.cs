﻿using Mirror;
using UnityEngine;

public class PlayerSounds : NetworkBehaviour
{
    public enum PlayerSoundType
    {
        Jump,
        Thok,
        RingDrop,
        NumLimit = 8
    }

    [Header("Sounds")]
    public GameSound jumpSound = new GameSound();
    public GameSound thokSound = new GameSound();
    public GameSound ringDropSound = new GameSound();

    // current layout: NNsssSSS where N = number (looping) 
    const int kNumCountBits = 2;
    const int kCountMask = (~0 << kNumSoundBitsTotal);
    const int kSoundMask = (int)(PlayerSoundType.NumLimit - 1);
    const int kNumSoundBitsTotal = 6;
    const int kNumSoundBitsSingle = 3;

    public byte soundHistory { get; private set; }

    private byte lastReceivedSoundHistory;

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