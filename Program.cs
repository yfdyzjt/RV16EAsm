namespace RV16EAsm
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Use: rveasm <InputFile> <OutputFile>");
                return;
            }

            string inputFile = args[0];
            string outputFile = args[1];

            try
            {
                var assembler = new Assembler();
                byte[] machineCode = assembler.Assemble(File.ReadAllText(inputFile));
                File.WriteAllBytes(outputFile, machineCode);
                Console.WriteLine($"Assembly success: {inputFile} -> {outputFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}