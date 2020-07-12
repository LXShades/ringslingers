using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
public class WorldSharedReferenceAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public class WorldIgnoreReferenceAttribute : Attribute { }

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class WorldClonableAttribute : Attribute { }