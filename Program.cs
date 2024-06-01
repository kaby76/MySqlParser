using Antlr4.Runtime;
using WPF_DbTools.Antlr;

var sql = "SELECT * FROM table_a AS a INNER JOIN table_b AS b ON a.field_1=b.field_2";
var charStream = CharStreams.fromString(sql);
var lexer = new MySQLLexer(charStream);
var stream = new CommonTokenStream(lexer);
var parser = new MySQLParser(stream);
parser.RemoveErrorListeners();
parser.AddErrorListener(new MySQLParserErrorListener());
for (int i = 0; ; ++i)
{
	var ro_token = lexer.NextToken();
	var token = (CommonToken)ro_token;
	token.TokenIndex = i;
	System.Console.WriteLine(token.ToString());
	if (token.Type == Antlr4.Runtime.TokenConstants.EOF)
		break;
}
lexer.Reset();
var tree = parser.queryMulti();