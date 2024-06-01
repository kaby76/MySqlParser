using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;

namespace WPF_DbTools.Antlr
{
    public abstract class MySQLBaseRecognizer : Parser
    {
        public int serverVersion = 0;

        public HashSet<SqlMode> sqlModes = new ();

        /** Enable Multi Language Extension support. */
        public bool supportMle = true;

        protected MySQLBaseRecognizer(ITokenStream input) : base(input)
        {
        }

        protected MySQLBaseRecognizer(ITokenStream input, TextWriter output, TextWriter errorOutput) : base(input, output, errorOutput)
        {
        }

        /**
         * Determines if the given SQL mode is currently active in the lexer.
         *
         * @param mode The mode to check.
         *
         * @returns True if the mode is one of the currently active modes.
         */
        public bool isSqlModeActive(SqlMode mode) {
            return this.sqlModes.Contains(mode);
        }
}
}
