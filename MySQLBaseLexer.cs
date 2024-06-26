﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Antlr4.Runtime;

namespace WPF_DbTools.Antlr
{
    public enum SqlMode
    {
        NoMode,
        AnsiQuotes,
        HighNotPrecedence,
        PipesAsConcat,
        IgnoreSpace,
        NoBackslashEscapes,
    }

    public abstract class MySQLBaseLexer : Lexer
    {
        public int serverVersion = 0;
        public HashSet<SqlMode> sqlModes = new HashSet<SqlMode>();

        /** Enable Multi Language Extension support. */
        public bool supportMle = true;

        public HashSet<string> charSets = new (); // Used to check repertoires.
        protected bool inVersionComment = false;

        private Queue<IToken> pendingTokens = new();

        static string longString = "2147483647";
        static int longLength = 10;
        static string signedLongString = "-2147483648";
        static string longLongString = "9223372036854775807";
        static int longLongLength = 19;
        static string signedLongLongString = "-9223372036854775808";
        static int signedLongLongLength = 19;
        static string unsignedLongLongString = "18446744073709551615";
        static int unsignedLongLongLength = 20;

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

        /**
         * Converts a mode string into individual mode flags.
         *
         * @param modes The input string to parse.
         */
	public void sqlModeFromString(string modes) {
            this.sqlModes.Clear();

            string[] parts = modes.ToUpper().Split(",");
            foreach (var mode in parts)
            {
                if (mode == "ANSI" || mode == "DB2" || mode == "MAXDB" || mode == "MSSQL" || mode == "ORACLE" ||
                    mode == "POSTGRESQL")
                {
                    this.sqlModes.Add(SqlMode.AnsiQuotes);
                    this.sqlModes.Add(SqlMode.PipesAsConcat);
                    this.sqlModes.Add(SqlMode.IgnoreSpace);
                } else if (mode == "ANSI_QUOTES") {
                    this.sqlModes.Add(SqlMode.AnsiQuotes);
                } else if (mode == "PIPES_AS_CONCAT") {
                    this.sqlModes.Add(SqlMode.PipesAsConcat);
                } else if (mode == "NO_BACKSLASH_ESCAPES") {
                    this.sqlModes.Add(SqlMode.NoBackslashEscapes);
                } else if (mode == "IGNORE_SPACE") {
                    this.sqlModes.Add(SqlMode.IgnoreSpace);
                } else if (mode == "HIGH_NOT_PRECEDENCE" || mode == "MYSQL323" || mode == "MYSQL40") {
                    this.sqlModes.Add(SqlMode.HighNotPrecedence);
                }
            };
        }

        /**
         * Resets the lexer by setting initial values to transient member, resetting the input stream position etc.
         */
        public override void Reset() 
        {
            this.inVersionComment = false;
            base.Reset();
        }

        /**
         * Implements the multi token feature required in our lexer.
         * A lexer rule can emit more than a single token, if needed.
         *
         * @returns The next token in the token stream.
         */
        public override IToken NextToken()
        {
            // First respond with pending tokens to the next token request, if there are any.
            if (pendingTokens.Count > 0)
            {
                var pending = this.pendingTokens.Dequeue();
                if (pending != null)
                {
                    return pending;
                }
            }

            // Let the main lexer class run the next token recognition.
            // This might create additional tokens again.
            IToken next = base.NextToken();
            if (pendingTokens.Count > 0)
            {
                var pending = this.pendingTokens.Dequeue();
                if (pending != null)
                {
                    this.pendingTokens.Enqueue(next);

                    return pending;
                }
            }

            return next;
        }

        /**
         * Checks if the version number in the token text is less than or equal to the current server version.
         *
         * @param text The text from a matched token.
         * @returns True if so the number matches, otherwise false.
         */
        protected bool checkMySQLVersion(string text) {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            if (text.Length < 8) {// Minimum is: /*!12345
                return false;
            }

            // Skip version comment introducer.
            int version = Int32.Parse(text.Substring(3));
            if (version <= this.serverVersion) {
                this.inVersionComment = true;

                return true;
            }

            return false;
        }

        /**
         * Called when a keyword was consumed that represents an internal MySQL function and checks if that keyword is
         * followed by an open parenthesis. If not then it is not considered a keyword but treated like a normal identifier.
         *
         * @param proposed The token type to use if the check succeeds.
         *
         * @returns If a function call is found then return the proposed token type, otherwise just IDENTIFIER.
         */
        protected int determineFunction(int proposed) {
            // Skip any whitespace character if the sql mode says they should be ignored,
            // before actually trying to match the open parenthesis.
            var c = (char)(InputStream.LA(1));
            if (this.isSqlModeActive(SqlMode.IgnoreSpace)) {
                while (c == ' ' || c == '\t' || c == '\r' || c == '\n') {
                    Interpreter.Consume((this as ITokenSource).InputStream);
                    Channel = Lexer.Hidden;
                    Type = MySQLLexer.WHITESPACE;
                    c = (char)(InputStream.LA(1));
                }
            }

            return c == '(' ? proposed : MySQLLexer.IDENTIFIER;

        }

        /**
         * Checks the given text and determines the smallest number type from it. Code has been taken from sql_lex.cc.
         *
         * @param text The text to parse (which must be a number).
         *
         * @returns The token type for that text.
         */
        protected int determineNumericType(string text) {
            // The original code checks for leading +/- but actually that can never happen, neither in the
            // server parser (as a digit is used to trigger processing in the lexer) nor in our parser
            // as our rules are defined without signs. But we do it anyway for maximum compatibility.
            if (string.IsNullOrEmpty(text))
            {
                return MySQLLexer.INT_NUMBER;
            }
            
            var length = text.Length - 1;
            if (length < MySQLBaseLexer.longLength) { // quick normal case
                return MySQLLexer.INT_NUMBER;
            }

            var negative = false;
            var index = 0;
            if (text[index] == '+') { // Remove sign and pre-zeros
                ++index;
                --length;
            } else if (text[index] == '-') {
                ++index;
                --length;
                negative = true;
            }

            while (text[index] == '0' && length > 0) {
                ++index;
                --length;
            }

            if (length < MySQLBaseLexer.longLength) {
                return MySQLLexer.INT_NUMBER;
            }

            int smaller = 0;
            int bigger = 0;
            string cmp;
            if (negative) {
                if (length == MySQLBaseLexer.longLength) {
                    cmp = MySQLBaseLexer.signedLongString.Substring(1);
                    smaller = MySQLLexer.INT_NUMBER; // If <= signed_long_str
                    bigger = MySQLLexer.LONG_NUMBER; // If >= signed_long_str
                } else if (length < MySQLBaseLexer.signedLongLongLength) {
                    return MySQLLexer.LONG_NUMBER;
                } else if (length > MySQLBaseLexer.signedLongLongLength) {
                    return MySQLLexer.DECIMAL_NUMBER;
                } else {
                    cmp = MySQLBaseLexer.signedLongLongString.Substring(1);
                    smaller = MySQLLexer.LONG_NUMBER; // If <= signed_longlong_str
                    bigger = MySQLLexer.DECIMAL_NUMBER;
                }
            } else {
                if (length == MySQLBaseLexer.longLength) {
                    cmp = MySQLBaseLexer.longString;
                    smaller = MySQLLexer.INT_NUMBER;
                    bigger = MySQLLexer.LONG_NUMBER;
                } else if (length < MySQLBaseLexer.longLongLength) {
                    return MySQLLexer.LONG_NUMBER;
                } else if (length > MySQLBaseLexer.longLongLength) {
                    if (length > MySQLBaseLexer.unsignedLongLongLength) {
                        return MySQLLexer.DECIMAL_NUMBER;
                    }
                    cmp = MySQLBaseLexer.unsignedLongLongString;
                    smaller = MySQLLexer.ULONGLONG_NUMBER;
                    bigger = MySQLLexer.DECIMAL_NUMBER;
                } else {
                    cmp = MySQLBaseLexer.longLongString;
                    smaller = MySQLLexer.LONG_NUMBER;
                    bigger = MySQLLexer.ULONGLONG_NUMBER;
                }
            }

            var otherIndex = 0;
            while (index < text.Length && cmp[otherIndex++] == text[index++]) {
                //
            }

            return text[index - 1] <= cmp[otherIndex - 1] ? smaller : bigger;
        }

        /**
         * Checks if the given text corresponds to a charset defined in the server (text is preceded by an underscore).
         *
         * @param text The text to check.
         *
         * @returns UNDERSCORE_CHARSET if so, otherwise IDENTIFIER.
         */
        protected int checkCharset(string text) {
            return this.charSets.Contains(text) ? MySQLLexer.UNDERSCORE_CHARSET : MySQLLexer.IDENTIFIER;
        }

        /**
         * Creates a DOT token in the token stream.
         */
        protected void emitDot()
        {
            this.pendingTokens.Enqueue(TokenFactory.Create(Tuple.Create((ITokenSource)this, (this as ITokenSource).InputStream), MySQLLexer.DOT_SYMBOL,
                ".", Channel, TokenStartCharIndex, TokenStartCharIndex, Line,
                Column
            ));

	    this.pendingTokens.Enqueue(TokenFactory.Create(Tuple.Create((ITokenSource)this, (this as ITokenSource).InputStream), MySQLLexer.IDENTIFIER,
		    Text.Substring(1), Channel, TokenStartCharIndex+1, TokenStartCharIndex+Text.Substring(1).Length, Line,
		    Column+1
		    ));

        }

        protected MySQLBaseLexer(ICharStream input) : base(input)
        {
        }

        protected MySQLBaseLexer(ICharStream input, TextWriter output, TextWriter errorOutput) : base(input, output, errorOutput)
        {
        }
    }
}
