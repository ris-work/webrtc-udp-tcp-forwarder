using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Rishi.Kexd;

public class MungedSDP{
    [JsonInclude] public required string sdp;
    [JsonInclude] public required string type;
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(MungedSDP))]
internal partial class MSDPC : JsonSerializerContext { }