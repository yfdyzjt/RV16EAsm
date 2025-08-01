using System.Text.RegularExpressions;

namespace RV16EAsm
{
    public class Assembler
    {
        private enum InstructionFormat
        {
            R,  // Register
            I,  // Immediate
            SB, // Store and branch
            UJ  // Long immediate and jump
        }

        private class Instruction
        {
            public string Mnemonic { get; set; }
            public InstructionFormat Format { get; set; }
            public int Opcode { get; set; }
            public int Funct3 { get; set; } = -1;

            public Instruction(string mnemonic, InstructionFormat format, int opcode, int funct3 = -1)
            {
                Mnemonic = mnemonic;
                Format = format;
                Opcode = opcode;
                Funct3 = funct3;
            }
        }

        private readonly Dictionary<string, int> registers = new(StringComparer.OrdinalIgnoreCase)
        {
            { "x0", 0 }, { "zero", 0 },
            { "x1", 1 }, { "ra", 1 },
            { "x2", 2 }, { "sp", 2 },
            { "x3", 3 }, { "t0", 3 },
            { "x4", 4 }, { "t1", 4 },
            { "x5", 5 }, { "t2", 5 },
            { "x6", 6 }, { "t3", 6 },
            { "x7", 7 }, { "t4", 7 },
        };

        private readonly List<Instruction> instructionSet = new()
        {
			// R-type
			new("add", InstructionFormat.R, 0b0001, 0b000),
            new("sub", InstructionFormat.R, 0b0001, 0b001),
            new("slt", InstructionFormat.R, 0b0001, 0b010),
            new("sltu", InstructionFormat.R, 0b0001, 0b011),
            new("xor", InstructionFormat.R, 0b0001, 0b100),
            new("or", InstructionFormat.R, 0b0001, 0b101),
            new("and", InstructionFormat.R, 0b0001, 0b110),
            new("sll", InstructionFormat.R, 0b1000, 0b000),
            new("srl", InstructionFormat.R, 0b1000, 0b010),
            new("sra", InstructionFormat.R, 0b1000, 0b011),
			
			// I-type
			new("addi", InstructionFormat.I, 0b0000, 0b000),
            new("slti", InstructionFormat.I, 0b0010, 0b000),
            new("sltiu", InstructionFormat.I, 0b0011, 0b000),
            new("xori", InstructionFormat.I, 0b0100, 0b000),
            new("ori", InstructionFormat.I, 0b0101, 0b000),
            new("andi", InstructionFormat.I, 0b0110, 0b000),
            new("slli", InstructionFormat.I, 0b1001, 0b000),
            new("srli", InstructionFormat.I, 0b1001, 0b100),
            new("srai", InstructionFormat.I, 0b1001, 0b110),
            new("lh", InstructionFormat.I, 0b1100, 0b000),
            new("jalr", InstructionFormat.I, 0b1010, 0b000),
			
			// SB-type
			new("sh", InstructionFormat.SB, 0b1101, 0b000),
            new("beq", InstructionFormat.SB, 0b1110, 0b000),
            new("bne", InstructionFormat.SB, 0b1111, 0b000),
			
			// U-type
			new("lui", InstructionFormat.UJ, 0b0111, 0b000),
            new("jal", InstructionFormat.UJ, 0b1011, 0b000)
        };

        private readonly Dictionary<string, int> labels = new();

        private readonly Dictionary<int, int> oringinLineToLineMap = new();
        private readonly Dictionary<int, int> addressToLineMap = new();

        private int currentAddress = 0;

        public byte[] Assemble(string sourceCode)
        {
            labels.Clear();
            oringinLineToLineMap.Clear();
            addressToLineMap.Clear();
            currentAddress = 0;

            var lines = PreprocessSourceCode(sourceCode);

            CollectLabels(lines);

            foreach(var label in labels)
            {
                Console.WriteLine($"{label.Key}: 0x{label.Value:X}");
            }

            return GenerateMachineCode(lines);
        }

        private List<string> PreprocessSourceCode(string sourceCode)
        {
            var result = new List<string>();
            var lines = sourceCode.Split('\n');
            bool inMultilineComment = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string processedLine = line;

                if (inMultilineComment)
                {
                    int endIndex = processedLine.IndexOf("*/");
                    if (endIndex >= 0)
                    {
                        processedLine = processedLine[(endIndex + 2)..];
                        inMultilineComment = false;
                    }
                    else
                    {
                        continue;
                    }
                }

                while (!inMultilineComment && processedLine.Length > 0)
                {
                    int singleLineCommentIndex = processedLine.IndexOf('#');
                    int multiLineCommentStartIndex = processedLine.IndexOf("/*");

                    if (singleLineCommentIndex >= 0 && (multiLineCommentStartIndex < 0 || singleLineCommentIndex < multiLineCommentStartIndex))
                    {
                        processedLine = processedLine[..singleLineCommentIndex];
                        break;
                    }
                    else if (multiLineCommentStartIndex >= 0)
                    {
                        string beforeComment = processedLine[..multiLineCommentStartIndex];
                        processedLine = processedLine[(multiLineCommentStartIndex + 2)..];

                        int endIndex = processedLine.IndexOf("*/");
                        if (endIndex >= 0)
                        {
                            processedLine = beforeComment + processedLine[(endIndex + 2)..];
                        }
                        else
                        {
                            processedLine = beforeComment;
                            inMultilineComment = true;
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                processedLine = processedLine.Trim();

                if (!string.IsNullOrWhiteSpace(processedLine))
                {
                    oringinLineToLineMap[result.Count] = i;
                    result.Add(processedLine);
                }
            }

            return result;
        }

        private void CollectLabels(List<string> lines)
        {
            int address = 0;
            int lineIndex = 0;

            foreach (var line in lines)
            {
                var labelMatch = Regex.Match(line, @"^([\w.]+):\s*$");
                if (labelMatch.Success)
                {
                    string labelName = labelMatch.Groups[1].Value;
                    labels[labelName] = address;
                }
                else
                {
                    addressToLineMap[address] = oringinLineToLineMap[lineIndex];
                    address += 1;
                }
                lineIndex++;
            }
        }

        private byte[] GenerateMachineCode(List<string> lines)
        {
            var machineCode = new List<byte>();
            currentAddress = 0;

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"^[\w.]+:\s*$"))
                {
                    continue;
                }
                else
                {
                    var instructionBytes = ParseInstruction(line);
                    machineCode.AddRange(instructionBytes);
                    currentAddress += 1;
                }
            }

            return machineCode.ToArray();
        }

        private byte[] ParseInstruction(string line)
        {
            try
            {
                var match = Regex.Match(line, @"^\s*([\w.]+)\s*(.*)$");
                if (!match.Success)
                    throw new Exception($"Invalid instruction format: {line}");

                string mnemonic = match.Groups[1].Value.ToLower();
                string operandsStr = match.Groups[2].Value.Trim();

                var instruction = instructionSet.FirstOrDefault(i => i.Mnemonic == mnemonic)
                    ?? throw new Exception($"Unknown instruction: {mnemonic}");

                var operands = ParseOperands(operandsStr);

                ushort machineCode = 0;

                machineCode = instruction.Format switch
                {
                    InstructionFormat.R => EncodeRType(instruction, operands),
                    InstructionFormat.I => EncodeIType(instruction, operands),
                    InstructionFormat.SB => EncodeSBType(instruction, operands),
                    InstructionFormat.UJ => EncodeUJType(instruction, operands),
                    _ => throw new Exception($"Unsupported instruction format: {instruction.Format}"),
                };

                return new byte[] { (byte)(machineCode & 0xFF), (byte)((machineCode >> 8) & 0xFF) };
            }
            catch (Exception ex)
            {
                if (addressToLineMap.TryGetValue(currentAddress, out int lineNumber))
                {
                    throw new Exception($"{ex.Message} at line {lineNumber + 1}");
                }
                throw;
            }
        }

        private static List<string> ParseOperands(string operandsStr)
        {
            if (string.IsNullOrWhiteSpace(operandsStr))
            {
                return new List<string>();
            }

            var operands = new List<string>();
            int start = 0;
            int parenthesesLevel = 0;

            for (int i = 0; i < operandsStr.Length; i++)
            {
                char c = operandsStr[i];

                if (c == '(' || c == '[')
                {
                    parenthesesLevel++;
                }
                else if (c == ')' || c == ']')
                {
                    parenthesesLevel--;
                }
                else if (c == ',' && parenthesesLevel == 0)
                {
                    operands.Add(operandsStr[start..i].Trim());
                    start = i + 1;
                }
            }

            operands.Add(operandsStr[start..].Trim());

            return operands;
        }

        private ushort EncodeRType(Instruction instruction, List<string> operands)
        {
            if (operands.Count != 3)
                throw new Exception($"R-type instruction requires 3 operands: {instruction.Mnemonic}");

            int rd = ParseRegister(operands[0]);
            int rs1 = ParseRegister(operands[1]);
            int rs2 = ParseRegister(operands[2]);

            ushort machineCode = 0;
            machineCode |= (ushort)(instruction.Opcode & 0xF);
            machineCode |= (ushort)((rd & 0x7) << 4);
            machineCode |= (ushort)((rs1 & 0x7) << 7);
            machineCode |= (ushort)((rs2 & 0x7) << 10);
            machineCode |= (ushort)((instruction.Funct3 & 0x7) << 13);

            return machineCode;
        }

        private (int offset, int rs) ParseOffsetAndBaseRegister(string operand)
        {
            var offsetBaseMatch = Regex.Match(operand, @"([^\(]+)\(([^\)]+)\)");
            if (!offsetBaseMatch.Success)
                throw new Exception($"Invalid offset format: {operand}");

            int offset = ParseImmediate(offsetBaseMatch.Groups[1].Value);
            int rs = ParseRegister(offsetBaseMatch.Groups[2].Value);

            return (offset, rs);
        }

        private ushort EncodeIType(Instruction instruction, List<string> operands)
        {
            if (instruction.Mnemonic == "lh"|| instruction.Mnemonic == "jalr")
            {
                if (operands.Count != 2)
                    throw new Exception($"{instruction.Mnemonic} instruction requires 2 operands: {instruction.Mnemonic} rd, offset(rs1)");

                int rd = ParseRegister(operands[0]);

                var (offset, rs1) = ParseOffsetAndBaseRegister(operands[1]);

                ushort machineCode = 0;
                machineCode |= (ushort)(instruction.Opcode & 0xF);
                machineCode |= (ushort)((rd & 0x7) << 4);
                machineCode |= (ushort)((rs1 & 0x7) << 7);

                if (offset < -32 || offset > 31)
                    throw new Exception($"Load offset out of range: {offset}");

                machineCode |= (ushort)((offset & 0x3F) << 10);

                return machineCode;
            }
            else
            {
                if (operands.Count != 3)
                    throw new Exception($"I-type instruction requires 3 operands: {instruction.Mnemonic}");

                int rd = ParseRegister(operands[0]);
                int rs1 = ParseRegister(operands[1]);
                int imm = ParseImmediate(operands[2]);

                ushort machineCode = 0;
                machineCode |= (ushort)(instruction.Opcode & 0xF);
                machineCode |= (ushort)((rd & 0x7) << 4);
                machineCode |= (ushort)((rs1 & 0x7) << 7);

                if (instruction.Mnemonic == "slli" || instruction.Mnemonic == "srli" || instruction.Mnemonic == "srai")
                {
                    if (imm < 0 || imm > 15)
                        throw new Exception($"Shift bits out of range: {imm}");

                    machineCode |= (ushort)((imm & 0xF) << 10);
                    machineCode |= (ushort)((instruction.Funct3 & 0x6) << 13);

                }
                else
                {
                    if (imm < -32 || imm > 31)
                        throw new Exception($"Immediate out of range: {imm}");

                    machineCode |= (ushort)((imm & 0x3F) << 10);
                }

                return machineCode;
            }
        }

        private ushort EncodeSBType(Instruction instruction, List<string> operands)
        {
            if (instruction.Mnemonic == "sh")
            {
                if (operands.Count != 2)
                    throw new Exception("sh instruction requires 2 operands: sh rs2, offset(rs1)");

                int rs2 = ParseRegister(operands[0]);

                var (offset, rs1) = ParseOffsetAndBaseRegister(operands[1]);

                ushort machineCode = 0;
                machineCode |= (ushort)(instruction.Opcode & 0xF);
                machineCode |= (ushort)((rs1 & 0x7) << 7);
                machineCode |= (ushort)((rs2 & 0x7) << 10);

                if (offset < -32 || offset > 31)
                    throw new Exception($"Store offset out of range: {offset}");

                machineCode |= (ushort)((offset & 0x7) << 4);
                machineCode |= (ushort)(((offset >> 3) & 0x7) << 13);

                return machineCode;
            }
            else
            {
                if (operands.Count != 3)
                    throw new Exception($"{instruction.Mnemonic} instruction requires 3 operands");

                int rs1 = ParseRegister(operands[0]);
                int rs2 = ParseRegister(operands[1]);
                int offset = CalculateOffset(operands[2]);

                ushort machineCode = 0;
                machineCode |= (ushort)(instruction.Opcode & 0xF);
                machineCode |= (ushort)((rs1 & 0x7) << 7);
                machineCode |= (ushort)((rs2 & 0x7) << 10);

                if (offset < -32 || offset > 31)
                    throw new Exception($"Branch offset out of range: {offset}");

                machineCode |= (ushort)((offset & 0x7) << 4);
                machineCode |= (ushort)(((offset >> 3) & 0x7) << 13);

                return machineCode;
            }
        }

        private ushort EncodeUJType(Instruction instruction, List<string> operands)
        {
            if (instruction.Mnemonic == "lui")
            {
                if (operands.Count != 2)
                    throw new Exception("lui instruction requires 2 operands: lui rd, imm");

                int rd = ParseRegister(operands[0]);
                int imm = ParseImmediate(operands[1]);

                ushort machineCode = 0;
                machineCode |= (ushort)(instruction.Opcode & 0xF);
                machineCode |= (ushort)((rd & 0x7) << 4);

                if (imm < -256 || imm > 255)
                    throw new Exception($"Immediate out of range: {imm}");

                machineCode |= (ushort)((imm & 0x1FF) << 7);

                return machineCode;
            }
            else
            {
                if (operands.Count != 2)
                    throw new Exception("jal instruction requires 2 operands: jal rd, offset");

                int rd = ParseRegister(operands[0]);
                int offset = CalculateOffset(operands[1]);

                ushort machineCode = 0;
                machineCode |= (ushort)(instruction.Opcode & 0xF);
                machineCode |= (ushort)((rd & 0x7) << 4);

                if (offset < -256 || offset > 255)
                    throw new Exception($"Jump offset out of range: {offset}");

                machineCode |= (ushort)((offset & 0x1FF) << 7);

                return machineCode;
            }
        }

        private int ParseRegister(string regStr)
        {
            regStr = regStr.Trim().ToLower();

            if (regStr.EndsWith(","))
                regStr = regStr[..^1];

            if (registers.TryGetValue(regStr, out int regNum))
                return regNum;

            throw new Exception($"Invalid register name: {regStr}");
        }

        private int ParseImmediate(string immStr)
        {
            immStr = immStr.Trim();

            if (immStr.EndsWith(","))
                immStr = immStr[..^1];

            if (labels.TryGetValue(immStr, out int labelAddress))
                return labelAddress;

            if (immStr.StartsWith("0x") || immStr.StartsWith("0X"))
            {
                if (int.TryParse(immStr.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out int hexValue))
                    return hexValue;
            }
            else if (immStr.StartsWith("0b") || immStr.StartsWith("0B"))
            {
                string binStr = immStr[2..];
                int binValue = 0;
                foreach (char c in binStr)
                {
                    if (c != '0' && c != '1')
                        throw new Exception($"Invalid binary number: {immStr}");
                    binValue = (binValue << 1) | (c - '0');
                }
                return binValue;
            }
            else
            {
                if (int.TryParse(immStr, out int decValue))
                    return decValue;
            }

            throw new Exception($"Invalid immediate value: {immStr}");
        }

        private int CalculateOffset(string target)
        {
            int offset;

            if (labels.TryGetValue(target, out int labelAddress))
            {
                offset = labelAddress - currentAddress;
            }
            else
            {
                offset = ParseImmediate(target);
            }

            return offset;
        }
    }
}