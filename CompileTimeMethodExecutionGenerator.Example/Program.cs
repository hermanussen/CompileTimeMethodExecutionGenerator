using System;
using System.Diagnostics;
using CompileTimeMethodExecutionGenerator;

namespace CompileTimeMethodExecutionGenerator.Example
{
    /// <summary>
    /// The following class will be extended during compilation by the source generator "CompileTimeMethodExecutionGenerator".
    /// To allow for that to happen, it has to be made partial.
    /// </summary>
    public partial class Calculator
    {
        /// <summary>
        /// This method does something that is cpu intensive and should always have the same result; it calculates pi in 20000 digits.
        /// 
        /// Of course, you could just run it once and place the resulting string in the method body to achieve the same result.
        /// But that would be more difficult to maintain.
        /// 
        /// Also, there is System.Math.PI. You should obviously use that if you want to do something with pi.
        /// But that's not the point here; this is a proof of concept example.
        /// </summary>
        [CompileTimeExecutor]
        public string Pi() {
            // Code derived from: https://stackoverflow.com/a/11679007/4624255
            const int digits = 20000;

            uint[] x = new uint[digits*10/3+2];
            uint[] r = new uint[digits*10/3+2];
            
            uint[] pi = new uint[digits];

            for (int j = 0; j < x.Length; j++)
                x[j] = 20;
                
            for (int i = 0; i < digits; i++)
            {
                uint carry = 0;
                for (int j = 0; j < x.Length; j++)
                {
                    uint num = (uint)(x.Length - j - 1);
                    uint dem = num * 2 + 1;

                    x[j] += carry;

                    uint q = x[j] / dem;
                    r[j] = x[j] % dem;

                    carry = q * num;
                }
                
                
                pi[i] = (x[x.Length-1] / 10);
                    
                            
                r[x.Length - 1] = x[x.Length - 1] % 10; ;
                
                for (int j = 0; j < x.Length; j++)
                    x[j] = r[j] * 10;
            }
            
            var result = "";
            
            uint c = 0;
            
            for(int i = pi.Length - 1; i >=0; i--)
            {
                pi[i] += c;
                c = pi[i] / 10;
                
                result = (pi[i] % 10).ToString() + result;
            }

            return result;
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            var calculator = new Calculator();

            // Execute the method like you normally would first and measure the time it takes
            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            Console.WriteLine($"Pi calculated with {calculator.Pi().Length} digits");
            stopWatch.Stop();
            Console.WriteLine($"Execution took {stopWatch.Elapsed.TotalMilliseconds}ms");

            // Now execute the compile time generated version of the same method
            stopWatch.Reset();
            stopWatch.Start();
            Console.WriteLine($"Pi calculated with {calculator.PiCompileTime().Length} digits (but performed calculation during compilation)");
            stopWatch.Stop();
            Console.WriteLine($"Execution took {stopWatch.Elapsed.TotalMilliseconds}ms");
        }
    }
}
