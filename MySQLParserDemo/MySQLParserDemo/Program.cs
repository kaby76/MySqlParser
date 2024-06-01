// See https://aka.ms/new-console-template for more information

using Antlr4.Runtime;
using WPF_DbTools.Antlr;

Console.WriteLine("Hello, World!");

string sql = "SELECT * FROM table_a AS A INNER JOIN table_b AS B ON a.field_1 = b.field_2";

ICharStream charStream = CharStreams.fromString(sql);
ITokenSource lexer = new MySQLLexer(charStream);
ITokenStream stream = new CommonTokenStream(lexer);
MySQLParser parser = new MySQLParser(stream);
parser.AddErrorListener(new MySQLParserErrorListener());
parser.BuildParseTree = true;
var treeAll = parser.queryMulti();