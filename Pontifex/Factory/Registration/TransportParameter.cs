using System;

namespace Pontifex.Factory.Registration
{
    /// <summary>
    /// A parameter declared by a transport constructor. Parameters are matched
    /// by their normalized, lowercase names.
    /// </summary>
    public abstract class TransportParameter
    {
        protected TransportParameter(string name, bool required, bool positional)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Required = required;
            Positional = positional;
        }

        public string Name { get; }

        /// <summary>
        /// The parameter must be present in the formula.
        /// </summary>
        public bool Required { get; }

        /// <summary>
        /// The parameter may be filled by a positional argument.
        /// </summary>
        public bool Positional { get; }
    }

    /// <summary>
    /// A decoded scalar string parameter.
    /// </summary>
    public sealed class StringParameter : TransportParameter
    {
        public StringParameter(
            string name,
            bool required,
            bool positional,
            ScalarValidator? validate = null)
            : base(name, required, positional)
        {
            Validate = validate;
        }

        public ScalarValidator? Validate { get; }
    }

    /// <summary>
    /// A parameter whose value is another transport expression.
    /// </summary>
    public sealed class NestedTransportParameter : TransportParameter
    {
        public NestedTransportParameter(
            string name,
            bool required,
            bool positional,
            TransportTypeSet expected)
            : base(name, required, positional)
        {
            Expected = expected;
        }

        public TransportTypeSet Expected { get; }
    }

    /// <summary>
    /// A parameter whose value is a list of transport expressions.
    /// </summary>
    public sealed class TransportListParameter : TransportParameter
    {
        public TransportListParameter(
            string name,
            bool required,
            bool positional,
            TransportTypeSet expected,
            bool homogeneous,
            int min,
            int? max = null)
            : base(name, required, positional)
        {
            if (min < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(min));
            }

            if (max.HasValue && max.Value < min)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(max),
                    "The maximum cannot be less than the minimum.");
            }

            Expected = expected;
            Homogeneous = homogeneous;
            Min = min;
            Max = max;
        }

        public TransportTypeSet Expected { get; }

        /// <summary>
        /// All elements must resolve to the same transport type.
        /// </summary>
        public bool Homogeneous { get; }

        public int Min { get; }

        /// <summary>
        /// The maximum element count, or <c>null</c> for no maximum.
        /// </summary>
        public int? Max { get; }
    }

    /// <summary>
    /// A parameter whose value is a struct with named fields.
    /// </summary>
    public sealed class StructParameter : TransportParameter
    {
        public StructParameter(
            string name,
            bool required,
            bool positional,
            StructSchema schema)
            : base(name, required, positional)
        {
            Schema = schema ?? throw new ArgumentNullException(nameof(schema));
        }

        public StructSchema Schema { get; }
    }
}
