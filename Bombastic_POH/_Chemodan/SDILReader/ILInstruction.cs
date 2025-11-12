using System;
using System.Text;
using System.Reflection.Emit;

namespace SDILReader
{
    public class ILInstruction
    {
        // Fields
        private OpCode code;
        private object operand;
        private byte[] operandData;
        private int offset;

        // Properties
        public OpCode Code
        {
            get { return code; }
            set { code = value; }
        }

        public object Operand
        {
            get { return operand; }
            set { operand = value; }
        }

        public byte[] OperandData
        {
            get { return operandData; }
            set { operandData = value; }
        }

        public int Offset
        {
            get { return offset; }
            set { offset = value; }
        }

        /// <summary>
        /// Returns a friendly strign representation of this instruction
        /// </summary>
        /// <returns></returns>
        public string GetCode()
        {
            StringBuilder result = new StringBuilder();
            result.Append(GetExpandedOffset(offset)).Append(" : ").Append(code);
            if (operand != null)
            {
                switch (code.OperandType)
                {
                    case OperandType.InlineField:
                        System.Reflection.FieldInfo fOperand = ((System.Reflection.FieldInfo)operand);
                        result.Append(" ")
                            .Append(Globals.ProcessSpecialTypes(fOperand.FieldType.ToString()))
                            .Append(" ")
                            .Append(Globals.ProcessSpecialTypes(fOperand.ReflectedType.ToString()))
                            .Append("::")
                            .Append(fOperand.Name)
                            .Append("");
                        break;
                    case OperandType.InlineMethod:
                        {
                            System.Reflection.MethodInfo methodInfo = operand as System.Reflection.MethodInfo;
                            if (methodInfo != null)
                            {
                                result.Append(" ");
                                if (!methodInfo.IsStatic) result.Append("instance ");
                                result.Append(Globals.ProcessSpecialTypes(methodInfo.ReturnType.ToString()))
                                    .Append(" ")
                                    .Append(Globals.ProcessSpecialTypes(methodInfo.ReflectedType.ToString()))
                                    .Append("::")
                                    .Append(methodInfo.Name)
                                    .Append("()");
                            }
                            else
                            {
                                System.Reflection.ConstructorInfo constructorInfo = operand as System.Reflection.ConstructorInfo;
                                if (constructorInfo != null)
                                {
                                    result.Append(" ");
                                    if (!constructorInfo.IsStatic) result.Append("instance ");
                                    result.Append("void ")
                                        .Append(Globals.ProcessSpecialTypes(constructorInfo.ReflectedType.ToString()))
                                        .Append("::")
                                        .Append(constructorInfo.Name)
                                        .Append("()");
                                }
                            }
                        }
                        break;
                    case OperandType.ShortInlineBrTarget:
                    case OperandType.InlineBrTarget:
                        result.Append(" ").Append(GetExpandedOffset((int)operand));
                        break;
                    case OperandType.InlineType:
                        result.Append(" ").Append(Globals.ProcessSpecialTypes(operand.ToString()));
                        break;
                    case OperandType.InlineString:
                        if (operand.ToString() == "\r\n")
                            result.Append(" \"\\r\\n\"");
                        else
                            result.Append(" \"")
                                .Append(operand.ToString())
                                .Append("\"");
                        break;
                    case OperandType.ShortInlineVar:
                        result.Append(operand.ToString());
                        break;
                    case OperandType.InlineI:
                    case OperandType.InlineI8:
                    case OperandType.InlineR:
                    case OperandType.ShortInlineI:
                    case OperandType.ShortInlineR:
                        result.Append(operand.ToString());
                        break;
                    case OperandType.InlineTok:
                        if (operand is Type)
                            result.Append(((Type)operand).FullName);
                        else
                            result.Append("not supported");
                        break;

                    default:
                        result.Append("not supported");
                        break;
                }
            }
            return result.ToString();

        }

        /// <summary>
        /// Add enough zeros to a number as to be represented on 4 characters
        /// </summary>
        /// <param name="offset">
        /// The number that must be represented on 4 characters
        /// </param>
        /// <returns>
        /// </returns>
        private string GetExpandedOffset(long offset)
        {
            if (999 < offset)
            {
                return offset.ToString();
            }
            else
            {
                StringBuilder result = new StringBuilder();
                if (99 < offset)
                {
                    result.Append("0");
                }
                else if (9 < offset)
                {
                    result.Append("00");
                }
                else
                {
                    result.Append("000");
                }
                result.Append(result);
                return result.ToString();
            }
        }

        public ILInstruction()
        {

        }
    }
}
