﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Commands.Test.IPC.Models;

internal sealed record class UnknownMessage(int SerializerId) : IRequest;
