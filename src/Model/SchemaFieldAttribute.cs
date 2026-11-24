namespace LoadingScreen.Model;

using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = true)]
public sealed class SchemaFieldAttribute : Attribute {
  public SchemaFieldAttribute(string name, string description) {
    Name = name;
    Description = description;
  }

  public string Name { get; }
  public string Description { get; }
  public string Form { get; set; }
  public Type ValueType { get; set; }
  public Type EnumType { get; set; }
  public bool LayerKind { get; set; }
  public int LayerKindOrder { get; set; }
  public double Minimum { get; set; } = double.NaN;
  public double Maximum { get; set; } = double.NaN;
  public bool ExclusiveMinimum { get; set; }
}
