﻿using System;
using System.Collections.Generic;

namespace Credfeto.Gallery.ObjectModel;

[Serializable]
public sealed class PhotoDeployment
{
    public Dictionary<string, Photo> Photos { get; } = new();
}