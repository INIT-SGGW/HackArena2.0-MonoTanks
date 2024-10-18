﻿using System.Net;
using System.Net.WebSockets;
using GameLogic.Networking;

namespace GameServer;

/// <summary>
/// Represents a connection with an unknown type.
/// </summary>
/// <param name="Context">The HTTP listener context.</param>
/// <param name="Socket">The WebSocket.</param>
/// <param name="EnumSerialization">The enum serialization format.</param>
internal record class UnknownConnection(
    HttpListenerContext Context,
    WebSocket Socket,
    EnumSerializationFormat EnumSerialization)
    : Connection(Context, Socket, new ConnectionData(EnumSerialization))
{
    /// <summary>
    /// Gets or sets the target type of the connection.
    /// </summary>
    public string TargetType { get; set; } = "Unknown";

    /// <inheritdoc/>
    public override string ToString()
    {
        return $"{base.ToString()}, Type=Unknown, TargetType={this.TargetType}";
    }
}
