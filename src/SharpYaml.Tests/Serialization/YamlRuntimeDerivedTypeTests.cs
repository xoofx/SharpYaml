#nullable enable

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpYaml.Serialization;

namespace SharpYaml.Tests.Serialization;

[TestClass]
public class YamlRuntimeDerivedTypeTests
{
    // ---- Model types: no [YamlDerivedType] attributes on base ----

    private abstract class Vehicle
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class Car : Vehicle
    {
        public int Doors { get; set; }
    }

    private sealed class Truck : Vehicle
    {
        public double PayloadTons { get; set; }
    }

    private sealed class Motorcycle : Vehicle
    {
        public bool HasSidecar { get; set; }
    }

    // Base class with [YamlPolymorphic] but no [YamlDerivedType]
    [YamlPolymorphic(DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag)]
    private abstract class Sensor
    {
        public string Id { get; set; } = string.Empty;
    }

    private sealed class TemperatureSensor : Sensor
    {
        public double MaxTemp { get; set; }
    }

    private sealed class PressureSensor : Sensor
    {
        public double MaxPsi { get; set; }
    }

    // Base with attribute-based derived types AND runtime derived types
    [YamlPolymorphic]
    [YamlDerivedType(typeof(Circle), "circle")]
    private abstract class Shape
    {
        public string Color { get; set; } = string.Empty;
    }

    private sealed class Circle : Shape
    {
        public double Radius { get; set; }
    }

    private sealed class Square : Shape
    {
        public double Side { get; set; }
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
    [JsonDerivedType(typeof(JsonCircle), "circle")]
    private abstract class JsonShape
    {
        public string Color { get; set; } = string.Empty;
    }

    private sealed class JsonCircle : JsonShape
    {
        public double Radius { get; set; }
    }

    private sealed class JsonSquare : JsonShape
    {
        public double Side { get; set; }
    }

    // Interface-based polymorphism
    private interface IPlugin
    {
        string Name { get; set; }
    }

    private sealed class AudioPlugin : IPlugin
    {
        public string Name { get; set; } = string.Empty;
        public int Channels { get; set; }
    }

    private sealed class VideoPlugin : IPlugin
    {
        public string Name { get; set; } = string.Empty;
        public int Width { get; set; }
    }

    // Default derived type (no discriminator)
    private abstract class Notification
    {
        public string Message { get; set; } = string.Empty;
    }

    private sealed class EmailNotification : Notification
    {
        public string To { get; set; } = string.Empty;
    }

    private sealed class DefaultNotification : Notification
    {
    }

    // Non-assignable type for validation test
    private sealed class Unrelated
    {
    }

    // ---- Property Discriminator Tests ----

    [TestMethod]
    public void RuntimeDerivedTypesDeserializeWithPropertyDiscriminator()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car"),
                        new YamlDerivedType(typeof(Truck), "truck"),
                    }
                }
            }
        };

        var yaml = "$type: car\nName: Sedan\nDoors: 4\n";
        var value = YamlSerializer.Deserialize<Vehicle>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Car>(value);
        Assert.AreEqual("Sedan", value.Name);
        Assert.AreEqual(4, ((Car)value).Doors);
    }

    [TestMethod]
    public void RuntimeDerivedTypesSerializeWithPropertyDiscriminator()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car"),
                        new YamlDerivedType(typeof(Truck), "truck"),
                    }
                }
            }
        };

        Vehicle vehicle = new Truck { Name = "Semi", PayloadTons = 20.5 };
        var yaml = YamlSerializer.Serialize(vehicle, typeof(Vehicle), options);

        StringAssert.Contains(yaml, "$type: truck");
        StringAssert.Contains(yaml, "Name: Semi");
        StringAssert.Contains(yaml, "PayloadTons: 20.5");
    }

    [TestMethod]
    public void RuntimeDerivedTypesDeserializeWithDiscriminatorNotFirst()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car"),
                    }
                }
            }
        };

        var yaml = "Name: Coupe\nDoors: 2\n$type: car\n";
        var value = YamlSerializer.Deserialize<Vehicle>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Car>(value);
        Assert.AreEqual("Coupe", value.Name);
        Assert.AreEqual(2, ((Car)value).Doors);
    }

    // ---- Tag Discriminator Tests ----

    [TestMethod]
    public void RuntimeDerivedTypesDeserializeWithTagDiscriminator()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car") { Tag = "!car" },
                        new YamlDerivedType(typeof(Truck), "truck") { Tag = "!truck" },
                    }
                }
            }
        };

        var yaml = "!car\nName: Roadster\nDoors: 2\n";
        var value = YamlSerializer.Deserialize<Vehicle>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Car>(value);
        Assert.AreEqual("Roadster", value.Name);
        Assert.AreEqual(2, ((Car)value).Doors);
    }

    [TestMethod]
    public void RuntimeDerivedTypesSerializeWithTagDiscriminator()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car") { Tag = "!car" },
                        new YamlDerivedType(typeof(Motorcycle), "moto") { Tag = "!moto" },
                    }
                }
            }
        };

        Vehicle vehicle = new Motorcycle { Name = "Harley", HasSidecar = true };
        var yaml = YamlSerializer.Serialize(vehicle, typeof(Vehicle), options);

        StringAssert.Contains(yaml, "!moto");
        Assert.IsFalse(yaml.Contains("$type:", StringComparison.Ordinal));
        StringAssert.Contains(yaml, "Name: Harley");
        StringAssert.Contains(yaml, "HasSidecar: true");
    }

    [TestMethod]
    public void RuntimeDerivedTypesDeserializeWithBothTagAndProperty()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Both,
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car") { Tag = "!car" },
                        new YamlDerivedType(typeof(Truck), "truck") { Tag = "!truck" },
                    }
                }
            }
        };

        var yaml = "!truck\nName: Pickup\nPayloadTons: 1.5\n";
        var value = YamlSerializer.Deserialize<Vehicle>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Truck>(value);
        Assert.AreEqual("Pickup", value.Name);
        Assert.AreEqual(1.5, ((Truck)value).PayloadTons);
    }

    // ---- Base type has [YamlPolymorphic] but no [YamlDerivedType] ----

    [TestMethod]
    public void BaseWithYamlPolymorphicAttributeUsesRuntimeDerivedTypes()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Sensor)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(TemperatureSensor)) { Tag = "!temp" },
                        new YamlDerivedType(typeof(PressureSensor)) { Tag = "!pressure" },
                    }
                }
            }
        };

        var yaml = "!temp\nId: S1\nMaxTemp: 100.5\n";
        var value = YamlSerializer.Deserialize<Sensor>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<TemperatureSensor>(value);
        Assert.AreEqual("S1", value.Id);
        Assert.AreEqual(100.5, ((TemperatureSensor)value).MaxTemp);
    }

    [TestMethod]
    public void BaseWithYamlPolymorphicAttributeSerializesWithRuntimeDerivedTypes()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Sensor)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(TemperatureSensor)) { Tag = "!temp" },
                        new YamlDerivedType(typeof(PressureSensor)) { Tag = "!pressure" },
                    }
                }
            }
        };

        Sensor sensor = new PressureSensor { Id = "P1", MaxPsi = 300.0 };
        var yaml = YamlSerializer.Serialize(sensor, typeof(Sensor), options);

        StringAssert.Contains(yaml, "!pressure");
        StringAssert.Contains(yaml, "Id: P1");
        StringAssert.Contains(yaml, "MaxPsi: 300");
    }

    // ---- Mixed: Attribute-based + Runtime-based ----

    [TestMethod]
    public void RuntimeDerivedTypeMergesWithAttributeDerivedTypes()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Shape)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Square), "square"),
                    }
                }
            }
        };

        // Attribute-registered type still works
        var yaml1 = "$type: circle\nColor: red\nRadius: 5\n";
        var circle = YamlSerializer.Deserialize<Shape>(yaml1, options);
        Assert.IsNotNull(circle);
        Assert.IsInstanceOfType<Circle>(circle);
        Assert.AreEqual("red", circle.Color);
        Assert.AreEqual(5.0, ((Circle)circle).Radius);

        // Runtime-registered type also works
        var yaml2 = "$type: square\nColor: blue\nSide: 3\n";
        var square = YamlSerializer.Deserialize<Shape>(yaml2, options);
        Assert.IsNotNull(square);
        Assert.IsInstanceOfType<Square>(square);
        Assert.AreEqual("blue", square.Color);
        Assert.AreEqual(3.0, ((Square)square).Side);
    }

    [TestMethod]
    public void RuntimeDerivedTypeSerializesMixedAttributeAndRuntime()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Shape)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Square), "square"),
                    }
                }
            }
        };

        Shape shape = new Square { Color = "green", Side = 7 };
        var yaml = YamlSerializer.Serialize(shape, typeof(Shape), options);

        StringAssert.Contains(yaml, "$type: square");
        StringAssert.Contains(yaml, "Color: green");
        StringAssert.Contains(yaml, "Side: 7");
    }

    [TestMethod]
    public void AttributeDerivedTypeTakesPrecedenceOverRuntime()
    {
        // Both attribute and runtime register Circle, attribute should win
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Shape)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Circle), "circle"), // same as attribute
                        new YamlDerivedType(typeof(Square), "square"),
                    }
                }
            }
        };

        var yaml = "$type: circle\nColor: yellow\nRadius: 2\n";
        var value = YamlSerializer.Deserialize<Shape>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Circle>(value);
        Assert.AreEqual(2.0, ((Circle)value).Radius);
    }

    [TestMethod]
    public void ConflictingRuntimeDiscriminatorIsSkippedWhenAttributeAlreadyOwnsIt()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Shape)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Square), "circle"),
                    }
                }
            }
        };

        var value = YamlSerializer.Deserialize<Shape>("$type: circle\nColor: red\nRadius: 5\n", options);
        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Circle>(value);

        var exception = Assert.Throws<NotSupportedException>(
            () => YamlSerializer.Serialize<Shape>(new Square { Color = "blue", Side = 3 }, options));
        StringAssert.Contains(exception.Message, typeof(Square).ToString());
    }

    [TestMethod]
    public void JsonAttributeDerivedTypeTakesPrecedenceOverRuntime()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(JsonShape)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(JsonSquare), "circle"),
                    }
                }
            }
        };

        var value = YamlSerializer.Deserialize<JsonShape>("kind: circle\nColor: red\nRadius: 5\n", options);
        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<JsonCircle>(value);

        var exception = Assert.Throws<NotSupportedException>(
            () => YamlSerializer.Serialize<JsonShape>(new JsonSquare { Color = "blue", Side = 3 }, options));
        StringAssert.Contains(exception.Message, typeof(JsonSquare).ToString());
    }

    // ---- Integer Discriminator ----

    [TestMethod]
    public void RuntimeDerivedTypesWorkWithIntegerDiscriminator()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), 1),
                        new YamlDerivedType(typeof(Truck), 2),
                    }
                }
            }
        };

        var yaml = "$type: 1\nName: Compact\nDoors: 4\n";
        var value = YamlSerializer.Deserialize<Vehicle>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Car>(value);
        Assert.AreEqual("Compact", value.Name);
    }

    [TestMethod]
    public void RuntimeDerivedTypesSerializeWithIntegerDiscriminator()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), 1),
                        new YamlDerivedType(typeof(Truck), 2),
                    }
                }
            }
        };

        Vehicle vehicle = new Truck { Name = "Hauler", PayloadTons = 10 };
        var yaml = YamlSerializer.Serialize(vehicle, typeof(Vehicle), options);

        StringAssert.Contains(yaml, "$type: 2");
        StringAssert.Contains(yaml, "Name: Hauler");
    }

    // ---- Default Derived Type (no discriminator) ----

    [TestMethod]
    public void RuntimeDefaultDerivedTypeDeserializesWhenNoDiscriminator()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Notification)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(EmailNotification), "email"),
                        new YamlDerivedType(typeof(DefaultNotification)),
                    }
                }
            }
        };

        var yaml = "Message: Hello\n";
        var value = YamlSerializer.Deserialize<Notification>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<DefaultNotification>(value);
        Assert.AreEqual("Hello", value.Message);
    }

    [TestMethod]
    public void RuntimeDefaultDerivedTypeSerializesWithoutDiscriminator()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Notification)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(EmailNotification), "email"),
                        new YamlDerivedType(typeof(DefaultNotification)),
                    }
                }
            }
        };

        Notification notification = new DefaultNotification { Message = "Test" };
        var yaml = YamlSerializer.Serialize(notification, typeof(Notification), options);

        Assert.IsFalse(yaml.Contains("$type:", StringComparison.Ordinal));
        StringAssert.Contains(yaml, "Message: Test");
    }

    [TestMethod]
    public void RuntimeDefaultDerivedTypeDeserializesWithMatchingDiscriminator()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Notification)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(EmailNotification), "email"),
                        new YamlDerivedType(typeof(DefaultNotification)),
                    }
                }
            }
        };

        var yaml = "$type: email\nMessage: Hi\nTo: test@example.com\n";
        var value = YamlSerializer.Deserialize<Notification>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<EmailNotification>(value);
        Assert.AreEqual("Hi", value.Message);
        Assert.AreEqual("test@example.com", ((EmailNotification)value).To);
    }

    // ---- Unknown Discriminator Handling ----

    [TestMethod]
    public void RuntimeDerivedTypesUnknownDiscriminatorFailsByDefault()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car"),
                    }
                }
            }
        };

        var yaml = "$type: spaceship\nName: USS Enterprise\n";
        Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Vehicle>(yaml, options));
    }

    [TestMethod]
    public void RuntimeDerivedTypesUnknownDiscriminatorCanFallBackToBase()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                UnknownDerivedTypeHandling = YamlUnknownDerivedTypeHandling.FallBackToBase,
                DerivedTypeMappings =
                {
                    [typeof(Notification)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(EmailNotification), "email"),
                        new YamlDerivedType(typeof(DefaultNotification)),
                    }
                }
            }
        };

        var yaml = "$type: sms\nMessage: Unknown\n";
        var value = YamlSerializer.Deserialize<Notification>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<DefaultNotification>(value);
        Assert.AreEqual("Unknown", value.Message);
    }

    // ---- Custom Discriminator Property Name ----

    [TestMethod]
    public void RuntimeDerivedTypesWithCustomDiscriminatorPropertyName()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "kind",
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car"),
                        new YamlDerivedType(typeof(Truck), "truck"),
                    }
                }
            }
        };

        var yaml = "kind: truck\nName: BigRig\nPayloadTons: 30\n";
        var value = YamlSerializer.Deserialize<Vehicle>(yaml, options);

        Assert.IsNotNull(value);
        Assert.IsInstanceOfType<Truck>(value);
        Assert.AreEqual("BigRig", value.Name);
    }

    [TestMethod]
    public void RuntimeDerivedTypesSerializeWithCustomDiscriminatorPropertyName()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                TypeDiscriminatorPropertyName = "kind",
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car"),
                    }
                }
            }
        };

        Vehicle vehicle = new Car { Name = "Mini", Doors = 3 };
        var yaml = YamlSerializer.Serialize(vehicle, typeof(Vehicle), options);

        StringAssert.Contains(yaml, "kind: car");
        Assert.IsFalse(yaml.Contains("$type:", StringComparison.Ordinal));
    }

    // ---- Roundtrip Tests ----

    [TestMethod]
    public void RuntimeDerivedTypesPropertyDiscriminatorRoundtrip()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car"),
                        new YamlDerivedType(typeof(Truck), "truck"),
                        new YamlDerivedType(typeof(Motorcycle), "moto"),
                    }
                }
            }
        };

        Vehicle original = new Car { Name = "Tesla", Doors = 4 };
        var yaml = YamlSerializer.Serialize(original, typeof(Vehicle), options);
        var deserialized = YamlSerializer.Deserialize<Vehicle>(yaml, options);

        Assert.IsNotNull(deserialized);
        Assert.IsInstanceOfType<Car>(deserialized);
        Assert.AreEqual("Tesla", deserialized.Name);
        Assert.AreEqual(4, ((Car)deserialized).Doors);
    }

    [TestMethod]
    public void RuntimeDerivedTypesTagDiscriminatorRoundtrip()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car") { Tag = "!car" },
                        new YamlDerivedType(typeof(Motorcycle), "moto") { Tag = "!moto" },
                    }
                }
            }
        };

        Vehicle original = new Motorcycle { Name = "Ducati", HasSidecar = false };
        var yaml = YamlSerializer.Serialize(original, typeof(Vehicle), options);
        var deserialized = YamlSerializer.Deserialize<Vehicle>(yaml, options);

        Assert.IsNotNull(deserialized);
        Assert.IsInstanceOfType<Motorcycle>(deserialized);
        Assert.AreEqual("Ducati", deserialized.Name);
        Assert.AreEqual(false, ((Motorcycle)deserialized).HasSidecar);
    }

    // ---- Dictionary with Polymorphic Values ----

    [TestMethod]
    public void RuntimeDerivedTypesInDictionaryValues()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DiscriminatorStyle = YamlTypeDiscriminatorStyle.Tag,
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car") { Tag = "!car" },
                        new YamlDerivedType(typeof(Truck), "truck") { Tag = "!truck" },
                    }
                }
            }
        };

        var yaml = "fleet1: !car\n  Name: Sedan\n  Doors: 4\nfleet2: !truck\n  Name: Hauler\n  PayloadTons: 15\n";
        var value = YamlSerializer.Deserialize<Dictionary<string, Vehicle>>(yaml, options);

        Assert.IsNotNull(value);
        Assert.AreEqual(2, value.Count);
        Assert.IsInstanceOfType<Car>(value["fleet1"]);
        Assert.AreEqual(4, ((Car)value["fleet1"]).Doors);
        Assert.IsInstanceOfType<Truck>(value["fleet2"]);
        Assert.AreEqual(15.0, ((Truck)value["fleet2"]).PayloadTons);
    }

    // ---- Validation Tests ----

    [TestMethod]
    public void RuntimeDerivedTypeThrowsWhenTypeNotAssignable()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Unrelated), "unrelated"),
                    }
                }
            }
        };

        var yaml = "$type: unrelated\n";
        var ex = Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Vehicle>(yaml, options));
        Assert.IsInstanceOfType<InvalidOperationException>(ex.InnerException);
        StringAssert.Contains(ex.InnerException!.Message, "not assignable");
    }

    [TestMethod]
    public void YamlDerivedTypeConstructorThrowsOnNullType()
    {
        Assert.Throws<ArgumentNullException>(() => new YamlDerivedType(null!));
        Assert.Throws<ArgumentNullException>(() => new YamlDerivedType(null!, "disc"));
        Assert.Throws<ArgumentNullException>(() => new YamlDerivedType(null!, 1));
    }

    [TestMethod]
    public void YamlDerivedTypeConstructorThrowsOnNullDiscriminator()
    {
        Assert.Throws<ArgumentNullException>(() => new YamlDerivedType(typeof(Car), (string)null!));
    }

    [TestMethod]
    public void YamlDerivedTypePropertiesAreSetCorrectly()
    {
        var dt1 = new YamlDerivedType(typeof(Car));
        Assert.AreEqual(typeof(Car), dt1.DerivedType);
        Assert.IsNull(dt1.Discriminator);
        Assert.IsNull(dt1.Tag);

        var dt2 = new YamlDerivedType(typeof(Truck), "truck") { Tag = "!truck" };
        Assert.AreEqual(typeof(Truck), dt2.DerivedType);
        Assert.AreEqual("truck", dt2.Discriminator);
        Assert.AreEqual("!truck", dt2.Tag);

        var dt3 = new YamlDerivedType(typeof(Motorcycle), 42);
        Assert.AreEqual(typeof(Motorcycle), dt3.DerivedType);
        Assert.AreEqual("42", dt3.Discriminator);
    }

    // ---- Empty/No Runtime Mappings ----

    [TestMethod]
    public void EmptyRuntimeMappingsDoNotAffectExistingBehavior()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>()
                }
            }
        };

        // Empty list should not enable polymorphism — deserializing abstract type with
        // a discriminator should fail because there are no registered derived types.
        Assert.Throws<YamlException>(() => YamlSerializer.Deserialize<Vehicle>("$type: car\nName: X\n", options));
    }

    [TestMethod]
    public void NoRuntimeMappingsDefaultsAreEmpty()
    {
        var options = new YamlPolymorphismOptions();
        Assert.IsNotNull(options.DerivedTypeMappings);
        Assert.AreEqual(0, options.DerivedTypeMappings.Count);
    }

    // ---- Cross-Project Architecture Pattern Test ----

    [TestMethod]
    public void CrossProjectPolymorphismPatternWorks()
    {
        // This test simulates the cross-project architecture:
        // - Sensor (base) in Core project — has [YamlPolymorphic] for tag style, but no [YamlDerivedType]
        // - TemperatureSensor, PressureSensor in Network project — registered at runtime
        // - Composition happens here (Application project)

        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Sensor)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(TemperatureSensor)) { Tag = "!temp" },
                        new YamlDerivedType(typeof(PressureSensor)) { Tag = "!pressure" },
                    }
                }
            }
        };

        // Deserialize a dictionary of sensors (typical YAML config pattern)
        var yaml = "sensor1: !temp\n  Id: T1\n  MaxTemp: 200\nsensor2: !pressure\n  Id: P1\n  MaxPsi: 500\n";
        var sensors = YamlSerializer.Deserialize<Dictionary<string, Sensor>>(yaml, options);

        Assert.IsNotNull(sensors);
        Assert.AreEqual(2, sensors.Count);

        Assert.IsInstanceOfType<TemperatureSensor>(sensors["sensor1"]);
        Assert.AreEqual("T1", sensors["sensor1"].Id);
        Assert.AreEqual(200.0, ((TemperatureSensor)sensors["sensor1"]).MaxTemp);

        Assert.IsInstanceOfType<PressureSensor>(sensors["sensor2"]);
        Assert.AreEqual("P1", sensors["sensor2"].Id);
        Assert.AreEqual(500.0, ((PressureSensor)sensors["sensor2"]).MaxPsi);

        // Serialize back
        var outputYaml = YamlSerializer.Serialize(sensors, options);
        StringAssert.Contains(outputYaml, "!temp");
        StringAssert.Contains(outputYaml, "!pressure");
        StringAssert.Contains(outputYaml, "Id: T1");
        StringAssert.Contains(outputYaml, "Id: P1");
    }

    // ---- Multiple Base Types ----

    [TestMethod]
    public void MultipleBaseTypesCanHaveRuntimeMappings()
    {
        var options = new YamlSerializerOptions
        {
            PolymorphismOptions = new YamlPolymorphismOptions
            {
                DerivedTypeMappings =
                {
                    [typeof(Vehicle)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(Car), "car"),
                    },
                    [typeof(Notification)] = new List<YamlDerivedType>
                    {
                        new YamlDerivedType(typeof(EmailNotification), "email"),
                    }
                }
            }
        };

        var vehicleYaml = "$type: car\nName: Test\nDoors: 2\n";
        var vehicle = YamlSerializer.Deserialize<Vehicle>(vehicleYaml, options);
        Assert.IsInstanceOfType<Car>(vehicle);

        var notifYaml = "$type: email\nMessage: Hello\nTo: user@test.com\n";
        var notif = YamlSerializer.Deserialize<Notification>(notifYaml, options);
        Assert.IsInstanceOfType<EmailNotification>(notif);
    }
}
