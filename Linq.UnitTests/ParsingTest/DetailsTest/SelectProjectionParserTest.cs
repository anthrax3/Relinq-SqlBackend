using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using Rubicon.Collections;
using Rubicon.Data.Linq.Clauses;
using Rubicon.Data.Linq.DataObjectModel;
using Rubicon.Data.Linq.Parsing;
using Rubicon.Data.Linq.Parsing.Details;
using Rubicon.Data.Linq.Parsing.FieldResolving;
using Rubicon.Data.Linq.Parsing.Structure;
using Rubicon.Data.Linq.UnitTests.TestQueryGenerators;

namespace Rubicon.Data.Linq.UnitTests.ParsingTest.DetailsTest
{
  [TestFixture]
  public class SelectProjectionParserTest
  {
    private IQueryable<Student> _source;
    private JoinedTableContext _context;

    [SetUp]
    public void SetUp ()
    {
      _source = ExpressionHelper.CreateQuerySource();
      _context = new JoinedTableContext();
    }
    
    [Test]
    public void IdentityProjection ()
    {
      IQueryable<Student> query = SelectTestQueryGenerator.CreateSimpleQuery (_source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields();

      Assert.That (selectedFields.ToArray(), Is.EqualTo (new object[] {ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, null)}));
    }

    [Test]
    public void IdentityProjection_WithinSubQueryInWhere ()
    {
      IQueryable<Student> query = SelectTestQueryGenerator.CreateSimpleQuery (_source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery ();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.SubQueryInWhere);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields ();

      Assert.That (selectedFields.ToArray (), Is.EqualTo (new object[] { ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, typeof (Student).GetProperty ("ID"), null)}));
    }


    [Test]
    public void IdentityProjection_WithoutSelectExpression ()
    {
      IQueryable<Student> query = WhereTestQueryGenerator.CreateSimpleWhereQuery (_source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields();

      Assert.That (selectedFields.ToArray(), Is.EqualTo (new object[] {ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, null)}));
    }

    [Test]
    public void MemberAccessProjection ()
    {
      IQueryable<string> query = SelectTestQueryGenerator.CreateSimpleQueryWithProjection (_source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields();

      Assert.That (selectedFields.ToArray(),
          Is.EqualTo (new object[] {ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, typeof (Student).GetProperty ("First"))}));
    }

    [Test]
    public void NewExpressionProjection ()
    {
      IQueryable<Tuple<string, string>> query = SelectTestQueryGenerator.CreateSimpleQueryWithFieldProjection (_source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields();

      Assert.That (selectedFields.ToArray(), Is.EqualTo (
          new object[]
            {
                ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, typeof (Student).GetProperty ("First")),
                ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, typeof (Student).GetProperty ("Last"))
            }));
    }

    [Test]
    public void NonDbMemberAccessProjection ()
    {
      IQueryable<string> query = SelectTestQueryGenerator.CreateSimpleSelectWithNonDbProjection (_source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields();

      Assert.IsEmpty (selectedFields.ToArray());
    }

    [Test]
    [ExpectedException (typeof (ParserException), ExpectedMessage = "The select clause contains an expression that cannot be parsed",
        MatchType = MessageMatch.Contains)]
    public void NonEntityMemberAccessProjection ()
    {
      IQueryable<int> query = SelectTestQueryGenerator.CreateSimpleSelectWithNonEntityMemberAccess (_source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery).GetSelectedFields();
    }

    [Test]
    public void MultiFromProjection ()
    {
      IQueryable<Tuple<string, string, int>> query = MixedTestQueryGenerator.CreateMultiFromQueryWithProjection (_source, _source, _source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields();

      Assert.That (selectedFields.ToArray(), Is.EqualTo (
          new object[]
            {
                ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, typeof (Student).GetProperty ("First")),
                ExpressionHelper.CreateFieldDescriptor ((FromClauseBase) model.BodyClauses.First(),
                    typeof (Student).GetProperty ("Last")),
                ExpressionHelper.CreateFieldDescriptor ((FromClauseBase) model.BodyClauses.Last(), typeof (Student).GetProperty ("ID"))
            }));
    }

    [Test]
    [ExpectedException (typeof (ParserException), ExpectedMessage = "The select clause contains an expression that cannot be parsed",
        MatchType = MessageMatch.Contains)]
    public void SimpleQueryWithSpecialProjection ()
    {
      IQueryable<Tuple<Student, string, string, string>> query = SelectTestQueryGenerator.CreateSimpleQueryWithSpecialProjection (_source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields();

      Assert.That (selectedFields.ToArray(), Is.EqualTo (
          new object[]
            {
                ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, null),
                ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, typeof (Student).GetProperty ("Last")),
            }));
    }

    [Test]
    public void QueryWithUnaryBinaryLambdaInvocationConvertNewArrayExpression ()
    {
      IQueryable<string> query = SelectTestQueryGenerator.CreateUnaryBinaryLambdaInvocationConvertNewArrayExpressionQuery (_source);
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields();

      Assert.That (selectedFields.ToArray(), Is.EquivalentTo (
          new object[]
            {
                ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, typeof (Student).GetProperty ("First")),
                ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, typeof (Student).GetProperty ("Last")),
                ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, null),
                ExpressionHelper.CreateFieldDescriptor (model.MainFromClause, typeof (Student).GetProperty ("ID"))
            }));
    }

    [Test]
    public void JoinSelectClause ()
    {
      IQueryable<string> query = JoinTestQueryGenerator.CreateSimpleImplicitSelectJoin (ExpressionHelper.CreateQuerySource_Detail ());
      QueryModel parsedQuery = ExpressionHelper.ParseQuery (query);

      SelectProjectionParser parser = new SelectProjectionParser (parsedQuery, (SelectClause) parsedQuery.SelectOrGroupClause,
          StubDatabaseInfo.Instance,
          _context, ParseContext.TopLevelQuery);

      PropertyInfo relationMember = typeof (Student_Detail).GetProperty ("Student");
      IColumnSource studentDetailTable = parsedQuery.MainFromClause.GetFromSource (StubDatabaseInfo.Instance);
      Table studentTable = DatabaseInfoUtility.GetRelatedTable (StubDatabaseInfo.Instance, relationMember);
      Tuple<string, string> joinColumns = DatabaseInfoUtility.GetJoinColumnNames (StubDatabaseInfo.Instance, relationMember);

      SingleJoin join = new SingleJoin (new Column (studentDetailTable, joinColumns.A), new Column (studentTable, joinColumns.B));

      FieldSourcePath path = new FieldSourcePath (studentDetailTable, new[] {join});

      FieldDescriptor expectedField =
          new FieldDescriptor (typeof (Student).GetProperty ("First"), path, new Column (studentTable, "FirstColumn"));

      Assert.That (parser.GetSelectedFields(), Is.EqualTo (new object[] {expectedField}));
    }

    [Test]
    public void ParserUsesContext ()
    {
      Assert.AreEqual (0, _context.Count);
      JoinSelectClause();
      Assert.AreEqual (1, _context.Count);
    }

    [Test]
    public void SelectRelationMember ()
    {
      IQueryable<Student> query = SelectTestQueryGenerator.CreateRelationMemberSelectQuery (ExpressionHelper.CreateQuerySource_Detail());
      QueryParser parser = new QueryParser (query.Expression);
      QueryModel model = parser.GetParsedQuery ();
      SelectClause selectClause = (SelectClause) model.SelectOrGroupClause;

      SelectProjectionParser selectParser = new SelectProjectionParser (model, selectClause, StubDatabaseInfo.Instance, _context, ParseContext.TopLevelQuery);

      IEnumerable<FieldDescriptor> selectedFields = selectParser.GetSelectedFields ();
      PropertyInfo relationMember = typeof (Student_Detail).GetProperty ("Student");
      IColumnSource studentDetailTable = model.MainFromClause.GetFromSource (StubDatabaseInfo.Instance);
      FieldSourcePath path = new FieldSourcePathBuilder ().BuildFieldSourcePath (StubDatabaseInfo.Instance, _context, studentDetailTable, 
          new[] { relationMember });
      FieldDescriptor expected = new FieldDescriptor(relationMember, path, new Column(path.LastSource, "*"));

      Assert.That (selectedFields.ToArray (), Is.EqualTo (new object[] { expected } ));
    }
  }
}