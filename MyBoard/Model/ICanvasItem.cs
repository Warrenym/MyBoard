using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace MyBoard.Model
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(NoteItem), typeDiscriminator: "note")]
    [JsonDerivedType(typeof(ImageItem), typeDiscriminator: "image")]
    [JsonDerivedType(typeof(Board), typeDiscriminator: "board")]
    internal interface ICanvasItem
    {
        Guid Id { get; }
        double X { get; set; }
        double Y { get; set; }
    }
}
