using System;
using System.Collections.Generic;

namespace Pontifex.Factory.Registration
{
    /// <summary>
    /// A struct schema. Fields keep declaration order.
    /// </summary>
    public sealed class StructSchema
    {
        private readonly StructField[] _fields;

        public StructSchema(IReadOnlyList<StructField> fields)
        {
            if (fields is null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            _fields = new StructField[fields.Count];
            for (var i = 0; i < _fields.Length; i++)
            {
                _fields[i] = fields[i]
                    ?? throw new ArgumentException(
                        "A struct field cannot be null.",
                        nameof(fields));
            }
        }

        public IReadOnlyList<StructField> Fields => _fields;
    }

    /// <summary>
    /// A named struct field. Fields are never positional and have no defaults.
    /// </summary>
    public abstract class StructField
    {
        protected StructField(string name, bool required)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Required = required;
        }

        public string Name { get; }

        public bool Required { get; }
    }

    /// <summary>
    /// A decoded scalar string field.
    /// </summary>
    public sealed class StringField : StructField
    {
        public StringField(
            string name,
            bool required,
            ScalarValidator? validate = null)
            : base(name, required)
        {
            Validate = validate;
        }

        public ScalarValidator? Validate { get; }
    }

    /// <summary>
    /// A signed 64-bit integer field with an inclusive range.
    /// </summary>
    public sealed class IntegerField : StructField
    {
        public IntegerField(
            string name,
            bool required,
            IntegerType type)
            : base(name, required)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public IntegerType Type { get; }
    }

    /// <summary>
    /// A decimal field with an inclusive range.
    /// </summary>
    public sealed class DecimalField : StructField
    {
        public DecimalField(
            string name,
            bool required,
            DecimalType type)
            : base(name, required)
        {
            Type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public DecimalType Type { get; }
    }

    /// <summary>
    /// A field whose value is another struct.
    /// </summary>
    public sealed class NestedStructField : StructField
    {
        public NestedStructField(
            string name,
            bool required,
            StructSchema schema)
            : base(name, required)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public StructSchema Schema { get; }
    }
}
