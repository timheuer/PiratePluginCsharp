using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FunctionPlugin;
public class Message
{
    [System.Text.Json.Serialization.JsonPropertyName("role")]
    public string? Role { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("content")]
    public string? Content { get; set; }
    [System.Text.Json.Serialization.JsonPropertyName("copilot_references")]
    public object[]? References { get; set; }
}

public class Data
{
    [System.Text.Json.Serialization.JsonPropertyName("messages")]
    public Message[]? messages { get; set; }
}
