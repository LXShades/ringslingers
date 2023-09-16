using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct AnimatorBool
{
    public Animator owner;
    private int hash;

    public AnimatorBool(Animator owner, string name)
    {
        this.owner = owner;
        this.hash = Animator.StringToHash(name);
    }

    public bool value
    {
        get => owner.GetBool(hash);
        set => owner.SetBool(hash, value);
    }
}


public struct AnimatorFloat
{
    public Animator owner;
    private int hash;

    public AnimatorFloat(Animator owner, string name)
    {
        this.owner = owner;
        this.hash = Animator.StringToHash(name);
    }

    public float value
    {
        get => owner.GetFloat(hash);
        set => owner.SetFloat(hash, value);
    }
}

public struct AnimatorInt
{
    public Animator owner;
    private int hash;

    public AnimatorInt(Animator owner, string name)
    {
        this.owner = owner;
        this.hash = Animator.StringToHash(name);
    }

    public int value
    {
        get => owner.GetInteger(hash);
        set => owner.SetInteger(hash, value);
    }
}

public struct AnimatorTrigger
{
    public Animator owner;
    private int hash;

    public AnimatorTrigger(Animator owner, string name)
    {
        this.owner = owner;
        this.hash = Animator.StringToHash(name);
    }

    public void Set() => owner.SetTrigger(hash);
}

