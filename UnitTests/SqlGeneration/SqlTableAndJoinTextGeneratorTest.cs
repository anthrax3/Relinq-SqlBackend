// This file is part of the re-linq project (relinq.codeplex.com)
// Copyright (c) rubicon IT GmbH, www.rubicon.eu
// 
// re-linq is free software; you can redistribute it and/or modify it under 
// the terms of the GNU Lesser General Public License as published by the 
// Free Software Foundation; either version 2.1 of the License, 
// or (at your option) any later version.
// 
// re-linq is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with re-linq; if not, see http://www.gnu.org/licenses.
// 

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Remotion.Linq.Clauses.StreamedData;
using Remotion.Linq.SqlBackend.SqlGeneration;
using Remotion.Linq.SqlBackend.SqlStatementModel;
using Remotion.Linq.SqlBackend.SqlStatementModel.Resolved;
using Remotion.Linq.SqlBackend.SqlStatementModel.Unresolved;
using Remotion.Linq.SqlBackend.UnitTests.SqlStatementModel;
using Remotion.Linq.SqlBackend.UnitTests.TestDomain;
using Rhino.Mocks;

namespace Remotion.Linq.SqlBackend.UnitTests.SqlGeneration
{
  [TestFixture]
  public class SqlTableAndJoinTextGeneratorTest
  {
    private SqlCommandBuilder _commandBuilder;
    private ISqlGenerationStage _stageMock;
    private SqlTableAndJoinTextGenerator _generator;

    [SetUp]
    public void SetUp ()
    {
      _stageMock = MockRepository.GenerateStrictMock<ISqlGenerationStage>();
      _generator = new SqlTableAndJoinTextGenerator (_stageMock);
      _commandBuilder = new SqlCommandBuilder();
    }

    [Test]
    public void Build_ForTable ()
    {
      var appendedTable = CreateResolvedAppendedTable("Table", "t", JoinSemantics.Inner);
      _generator.Build (appendedTable, _commandBuilder, true);

      Assert.That (_commandBuilder.GetCommandText(), Is.EqualTo ("[Table] AS [t]"));
    }

    [Test]
    public void Build_ForSeveralTables ()
    {
      var sqlTable1 = CreateResolvedAppendedTable ("Table1", "t1", JoinSemantics.Inner);
      var sqlTable2 = CreateResolvedAppendedTable ("Table2", "t2", JoinSemantics.Inner);
      var sqlTable3 = CreateResolvedAppendedTable ("Table3", "t3", JoinSemantics.Inner);
      _generator.Build (sqlTable1, _commandBuilder, true);
      _generator.Build (sqlTable2, _commandBuilder, false);
      _generator.Build (sqlTable3, _commandBuilder, false);

      Assert.That (_commandBuilder.GetCommandText(), Is.EqualTo ("[Table1] AS [t1] CROSS JOIN [Table2] AS [t2] CROSS JOIN [Table3] AS [t3]"));
    }

    [Test]
    public void Build_ForLeftJoin ()
    {
      var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);

      var join = CreateResolvedJoin(typeof (Cook), "t1", JoinSemantics.Left, "ID", "CookTable", "t2", "FK");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join);

      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("([t1].[ID] = [t2].[FK])"));
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (
          _commandBuilder.GetCommandText(), Is.EqualTo ("[KitchenTable] AS [t1] LEFT OUTER JOIN [CookTable] AS [t2] ON ([t1].[ID] = [t2].[FK])"));
    }

    [Test]
    public void Build_ForLeftJoinWithoutJoinCondition_OptimizedToOuterApply ()
    {
      var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);

      var join = CreateResolvedJoinWithoutJoinCondition (typeof (Cook), JoinSemantics.Left, "CookTable", "t2");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join);

      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join.JoinCondition))
          .Repeat.Never();
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (_commandBuilder.GetCommandText(), Is.EqualTo ("[KitchenTable] AS [t1] OUTER APPLY [CookTable] AS [t2]"));
    }

    [Test]
    public void Build_ForLeftJoin_Multiple_WithJoinConditionAndWithoutJoinCondition()
    {
      var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);

      var join1 = CreateResolvedJoin(typeof (Cook), "t1", JoinSemantics.Left, "ID", "Table2", "t2", "FK");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join1);

      var join2 = CreateResolvedJoinWithoutJoinCondition (typeof (Cook), JoinSemantics.Left, "Table3", "t3");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join2);

      var join3 = CreateResolvedJoin(typeof (Cook), "t1", JoinSemantics.Left, "ID", "Table4", "t4", "FK");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join3);

      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join1.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("([t1].[ID] = [t2].[FK])"));
      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join2.JoinCondition))
          .Repeat.Never();
      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join3.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("([t1].[ID] = [t4].[FK])"));
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (
          _commandBuilder.GetCommandText(),
          Is.EqualTo (
              "[KitchenTable] AS [t1] "
              + "LEFT OUTER JOIN [Table2] AS [t2] ON ([t1].[ID] = [t2].[FK]) "
              + "OUTER APPLY [Table3] AS [t3] "
              + "LEFT OUTER JOIN [Table4] AS [t4] ON ([t1].[ID] = [t4].[FK])"));
    }

    [Test]
    public void Build_ForInnerJoin ()
    {
       var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);
       var join = CreateResolvedJoin (typeof (Cook), "t1", JoinSemantics.Inner, "ID", "CookTable", "t2", "FK");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join);

      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("([t1].[ID] = [t2].[FK])"));
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (
          _commandBuilder.GetCommandText(), Is.EqualTo ("[KitchenTable] AS [t1] INNER JOIN [CookTable] AS [t2] ON ([t1].[ID] = [t2].[FK])"));
    }

    [Test]
    public void Build_ForInnerJoinWithoutJoinCondition_WithResolvedSimpleTable_OptimizedToCrossJoin ()
    {
       var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);
       var join = CreateResolvedJoinWithoutJoinCondition (typeof (Cook), JoinSemantics.Inner, "CookTable", "t2");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join);

      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join.JoinCondition))
          .Repeat.Never();
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (
          _commandBuilder.GetCommandText(), Is.EqualTo ("[KitchenTable] AS [t1] CROSS JOIN [CookTable] AS [t2]"));
    }

    [Test]
    public void Build_ForInnerJoinWithoutJoinCondition_WithSubStatementOptimizedToCrossApply ()
    {
       var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);
       var join = CreateResolvedJoinForSubStatementTableInfoWithoutJoinCondition (typeof (Cook), JoinSemantics.Inner, "t2");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join);

      _stageMock
          .Expect (_ => _.GenerateTextForJoinCondition (_commandBuilder, join.JoinCondition))
          .Repeat.Never();
      _stageMock
          .Expect (_ => _.GenerateTextForSqlStatement (_commandBuilder, ((ResolvedSubStatementTableInfo) join.JoinedTable.TableInfo).SqlStatement))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("SubStatement"));
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (_commandBuilder.GetCommandText(), Is.EqualTo ("[KitchenTable] AS [t1] CROSS APPLY (SubStatement) AS [t2]"));
    }

    [Test]
    public void Build_ForInnerJoinWithoutJoinCondition_WithGroupingOptimizedToCrossApply ()
    {
       var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);
       var join = CreateResolvedJoinForJoinedGroupingTableInfoWithoutJoinCondition (typeof (Cook), JoinSemantics.Inner, "t2");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join);

      _stageMock
          .Expect (_ => _.GenerateTextForJoinCondition (_commandBuilder, join.JoinCondition))
          .Repeat.Never();
      _stageMock
          .Expect (_ => _.GenerateTextForSqlStatement (_commandBuilder, ((ResolvedJoinedGroupingTableInfo) join.JoinedTable.TableInfo).SqlStatement))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("SubStatement"));
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (_commandBuilder.GetCommandText(), Is.EqualTo ("[KitchenTable] AS [t1] CROSS APPLY (SubStatement) AS [t2]"));
    }

    [Test]
    public void Build_ForInnerJoin_Multiple_WithJoinConditionAndWithoutJoinCondition()
    {
      var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);

      var join1 = CreateResolvedJoin(typeof (Cook), "t1", JoinSemantics.Inner, "ID", "Table2", "t2", "FK");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join1);

      var join2 = CreateResolvedJoinWithoutJoinCondition (typeof (Cook), JoinSemantics.Inner, "Table3", "t3");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join2);

      var join3 = CreateResolvedJoin(typeof (Cook), "t1", JoinSemantics.Inner, "ID", "Table4", "t4", "FK");
       originalTable.SqlTable.AddJoinForExplicitQuerySource (join3);

      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join1.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("([t1].[ID] = [t2].[FK])"));
      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join2.JoinCondition))
          .Repeat.Never();
      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join3.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("([t1].[ID] = [t4].[FK])"));
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (
          _commandBuilder.GetCommandText(),
          Is.EqualTo (
              "[KitchenTable] AS [t1] "
              + "INNER JOIN [Table2] AS [t2] ON ([t1].[ID] = [t2].[FK]) "
              + "CROSS JOIN [Table3] AS [t3] "
              + "INNER JOIN [Table4] AS [t4] ON ([t1].[ID] = [t4].[FK])"));
    }

    [Test]
    public void Build_ForLeftJoin_WithJoinCondition_Recursive ()
    {
      var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);
      var join1 = CreateResolvedJoin(typeof (Cook), "t1", JoinSemantics.Left, "ID", "CookTable", "t2", "FK");
      originalTable.SqlTable.AddJoinForExplicitQuerySource (join1);
      var join2 = CreateResolvedJoin(typeof (Cook), "t2", JoinSemantics.Left, "ID2", "CookTable2", "t3", "FK2");
      join1.JoinedTable.AddJoinForExplicitQuerySource (join2);

      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join1.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("X"));
      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join2.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("Y"));
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (
          _commandBuilder.GetCommandText(),
          Is.EqualTo (
              "[KitchenTable] AS [t1] "
              + "LEFT OUTER JOIN [CookTable] AS [t2] "
              + "LEFT OUTER JOIN [CookTable2] AS [t3] "
              + "ON Y "
              + "ON X"));
    }

    [Test]
    public void Build_ForLeftJoin_WithoutJoinCondition_Recursive ()
    {
      var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);
      var join1 = CreateResolvedJoinWithoutJoinCondition (typeof (Cook), JoinSemantics.Left, "CookTable", "t2");
      originalTable.SqlTable.AddJoinForExplicitQuerySource (join1);
      var join2 = CreateResolvedJoin (typeof (Cook), "t2", JoinSemantics.Left, "ID2", "CookTable2", "t3", "FK2");
      join1.JoinedTable.AddJoinForExplicitQuerySource (join2);

      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join1.JoinCondition))
          .Repeat.Never();
      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join2.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("Y"));
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (
          _commandBuilder.GetCommandText(),
          Is.EqualTo (
              "[KitchenTable] AS [t1] "
              + "OUTER APPLY [CookTable] AS [t2] "
              + "LEFT OUTER JOIN [CookTable2] AS [t3] "
              + "ON Y"));
    }

    [Test]
    public void Build_ForLeftJoin_Multiple ()
    {
      var originalTable = CreateResolvedAppendedTable ("KitchenTable", "t1", JoinSemantics.Inner);
      var join1 = CreateResolvedJoin(typeof (Cook), "t1", JoinSemantics.Left, "ID", "CookTable", "t2", "FK");
      originalTable.SqlTable.AddJoinForExplicitQuerySource (join1);
      var join2 = CreateResolvedJoin(typeof (Cook), "t2", JoinSemantics.Left, "ID2", "CookTable2", "t3", "FK2");
      originalTable.SqlTable.AddJoinForExplicitQuerySource (join2);

      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join1.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("X"));
      _stageMock
          .Expect (mock => mock.GenerateTextForJoinCondition (_commandBuilder, join2.JoinCondition))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("Y"));
      _stageMock.Replay();

      _generator.Build (originalTable, _commandBuilder, true);

      _stageMock.VerifyAllExpectations();
      Assert.That (
          _commandBuilder.GetCommandText(),
          Is.EqualTo (
              "[KitchenTable] AS [t1] "
              + "LEFT OUTER JOIN [CookTable] AS [t2] ON X "
              + "LEFT OUTER JOIN [CookTable2] AS [t3] ON Y"));
    }

    [Test]
    public void Build_CrossJoinSemantics_FirstTable ()
    {
      var sqlTable = CreateResolvedAppendedTable ("Table", "t", JoinSemantics.Inner);

      _generator.Build (sqlTable, _commandBuilder, isFirstTable: true);
      
      Assert.That (_commandBuilder.GetCommandText (), Is.EqualTo ("[Table] AS [t]"));
    }

    [Test]
    public void Build_CrossJoinSemantics_NonFirstTable_SimpleTableInfo ()
    {
      var sqlTable = CreateResolvedAppendedTable ("Table", "t", JoinSemantics.Inner);

      _generator.Build (sqlTable, _commandBuilder, isFirstTable: false);

      Assert.That (_commandBuilder.GetCommandText (), Is.EqualTo (" CROSS JOIN [Table] AS [t]"));
    }

    [Test]
    public void Build_CrossJoinSemantics_NonFirstTable_SubstatementTableInfo ()
    {
      var sqlStatement = SqlStatementModelObjectMother.CreateSqlStatement_Resolved (typeof (Cook));
      var tableInfo = new ResolvedSubStatementTableInfo("q0", sqlStatement);
      var sqlTable = SqlStatementModelObjectMother.CreateSqlAppendedTable (tableInfo, JoinSemantics.Inner);
      
      _stageMock
        .Expect (mock => mock.GenerateTextForSqlStatement (_commandBuilder, sqlStatement))
        .WhenCalled(mi=> ((ISqlCommandBuilder) mi.Arguments[0]).Append("[Table] AS [t]"));
      _stageMock.Replay();

      _generator.Build (sqlTable, _commandBuilder, isFirstTable: false);

      _stageMock.VerifyAllExpectations();
      Assert.That (_commandBuilder.GetCommandText (), Is.EqualTo (" CROSS APPLY ([Table] AS [t]) AS [q0]"));
    }

    [Test]
    public void Build_OuterApplySemantics_FirstTable ()
    {
      var sqlTable = CreateResolvedAppendedTable ("Table", "t", JoinSemantics.Left);

      _generator.Build (sqlTable, _commandBuilder, isFirstTable: true);

      Assert.That (_commandBuilder.GetCommandText (), Is.EqualTo ("(SELECT NULL AS [Empty]) AS [Empty] OUTER APPLY [Table] AS [t]"));
    }

    [Test]
    public void Build_OuterApplySemantics_NonFirstTable ()
    {
      var sqlTable = CreateResolvedAppendedTable ("Table", "t", JoinSemantics.Left);

      _generator.Build (sqlTable, _commandBuilder, isFirstTable: false);

      Assert.That (_commandBuilder.GetCommandText (), Is.EqualTo (" OUTER APPLY [Table] AS [t]"));
    }

    [Test]
    public void Build_WithResolvedSimpleTableInfo ()
    {
      var simpleTableInfo = new ResolvedSimpleTableInfo (typeof (Cook), "CookTable", "c");

      _generator.Build (
          new SqlAppendedTable (new SqlTable (simpleTableInfo), JoinSemantics.Inner),
          _commandBuilder,
          isFirstTable: true);

      Assert.That (_commandBuilder.GetCommandText (), Is.EqualTo ("[CookTable] AS [c]"));
    }

    [Test]
    public void Build_WithResolvedSimpleTableInfo_FullQualifiedTableNameGetsSplit ()
    {
      var simpleTableInfo = new ResolvedSimpleTableInfo (typeof (Cook), "TestDomain.dbo.CookTable", "c");

      _generator.Build (
          new SqlAppendedTable (new SqlTable (simpleTableInfo), JoinSemantics.Inner),
          _commandBuilder,
          isFirstTable: true);

      Assert.That (_commandBuilder.GetCommandText(), Is.EqualTo ("[TestDomain].[dbo].[CookTable] AS [c]"));
    }

    [Test]
    public void Build_WithResolvedSubStatementTableInfo ()
    {
      var sqlStatement = new SqlStatementBuilder (SqlStatementModelObjectMother.CreateSqlStatement_Resolved (typeof (Cook[])))
      {
        DataInfo = new StreamedSequenceInfo (typeof (IQueryable<Cook>), Expression.Constant (new Cook ()))
      }.GetSqlStatement ();
      var resolvedSubTableInfo = new ResolvedSubStatementTableInfo ("cook", sqlStatement);

      _stageMock
          .Expect (mock => mock.GenerateTextForSqlStatement (_commandBuilder, sqlStatement))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("XXX"));
      _stageMock.Replay();

      _generator.Build (
          new SqlAppendedTable (new SqlTable (resolvedSubTableInfo), JoinSemantics.Inner),
          _commandBuilder,
          isFirstTable: true);

      _stageMock.VerifyAllExpectations();
      Assert.That (_commandBuilder.GetCommandText (), Is.EqualTo ("(XXX) AS [cook]"));
    }

    [Test]
    public void Build_WithResolvedJoinedGroupingTableInfo ()
    {
      var sqlStatement = new SqlStatementBuilder (SqlStatementModelObjectMother.CreateSqlStatement_Resolved (typeof (Cook[])))
      {
        DataInfo = new StreamedSequenceInfo (typeof (IQueryable<Cook>), Expression.Constant (new Cook ()))
      }.GetSqlStatement ();
      var resolvedJoinedGroupingTableInfo = SqlStatementModelObjectMother.CreateResolvedJoinedGroupingTableInfo (sqlStatement);

      _stageMock
          .Expect (mock => mock.GenerateTextForSqlStatement (_commandBuilder, sqlStatement))
          .WhenCalled (mi => ((SqlCommandBuilder) mi.Arguments[0]).Append ("XXX"));
      _stageMock.Replay ();

      _generator.Build (
          new SqlAppendedTable (new SqlTable (resolvedJoinedGroupingTableInfo), JoinSemantics.Inner),
          _commandBuilder,
          isFirstTable: true);

      _stageMock.VerifyAllExpectations ();
      Assert.That (_commandBuilder.GetCommandText (), Is.EqualTo ("(XXX) AS [cook]"));
    }

    [Test]
    public void Build_WithUnresolvedTableInfo_RaisesException ()
    {
      var appendedTable = SqlStatementModelObjectMother.CreateSqlAppendedTable (
          SqlStatementModelObjectMother.CreateSqlTable_WithUnresolvedTableInfo());
      Assert.That (
          () => _generator.Build (appendedTable, _commandBuilder, false),
          Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo ("UnresolvedTableInfo is not valid at this point."));
    }

    [Test]
    public void Build_WithUnresolvedJoinTableInfo_RaisesException ()
    {
      var appendedTable = SqlStatementModelObjectMother.CreateSqlAppendedTable (
          SqlStatementModelObjectMother.CreateUnresolvedJoinTableInfo());
      Assert.That (
          () => _generator.Build (appendedTable, _commandBuilder, false),
          Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo ("UnresolvedJoinTableInfo is not valid at this point."));
    }

    [Test]
    public void Build_WithUnresolvedCollectionJoinTableInfo_RaisesException ()
    {
      var appendedTable = SqlStatementModelObjectMother.CreateSqlAppendedTable (
          SqlStatementModelObjectMother.CreateUnresolvedCollectionJoinTableInfo());
      Assert.That (
          () => _generator.Build (appendedTable, _commandBuilder, false),
          Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo ("UnresolvedCollectionJoinTableInfo is not valid at this point."));
    }

    [Test]
    public void ApplyContext_VisitUnresolvedDummyRowTableInfo ()
    {
      var appendedTable = SqlStatementModelObjectMother.CreateSqlAppendedTable (
          SqlStatementModelObjectMother.CreateUnresolvedDummyRowTableInfo());

      Assert.That (
          () => _generator.Build (appendedTable, _commandBuilder, false),
          Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo ("UnresolvedDummyRowTableInfo is not valid at this point."));
    }

    [Test]
    public void Build_WithUnresolvedGroupReferenceTableInfo ()
    {
      var appendedTable = SqlStatementModelObjectMother.CreateSqlAppendedTable (
          SqlStatementModelObjectMother.CreateUnresolvedGroupReferenceTableInfo());
      Assert.That (
          () => _generator.Build (appendedTable, _commandBuilder, false),
          Throws.TypeOf<InvalidOperationException>().With.Message.EqualTo ("UnresolvedGroupReferenceTableInfo is not valid at this point."));
    }

    private SqlJoin CreateResolvedJoin (
        Type type,
        string originalTableAlias,
        JoinSemantics joinSemantics,
        string leftSideKeyName,
        string joinedTableName,
        string joinedTableAlias,
        string rightSideKeyName)
    {
      var joinedTableInfo = new ResolvedSimpleTableInfo (type, joinedTableName, joinedTableAlias);
      var joinedTable = new SqlTable (joinedTableInfo);

      var primaryColumn = new SqlColumnDefinitionExpression (typeof (int), originalTableAlias, leftSideKeyName, false);
      var foreignColumn = new SqlColumnDefinitionExpression (typeof (int), joinedTableAlias, rightSideKeyName, false);

      return new SqlJoin (joinedTable, joinSemantics, Expression.Equal (primaryColumn, foreignColumn));
    }

    private SqlJoin CreateResolvedJoinWithoutJoinCondition (Type type, JoinSemantics joinSemantics, string joinedTableName, string joinedTableAlias)
    {
      var joinedTableInfo = new ResolvedSimpleTableInfo (type, joinedTableName, joinedTableAlias);
      var joinedTable = new SqlTable (joinedTableInfo);

      return new SqlJoin (joinedTable, joinSemantics, new NullJoinConditionExpression());
    }

    private SqlJoin CreateResolvedJoinForSubStatementTableInfoWithoutJoinCondition (Type type, JoinSemantics joinSemantics, string joinedTableAlias)
    {
      var joinedTableInfo = new ResolvedSubStatementTableInfo (
          joinedTableAlias,
          SqlStatementModelObjectMother.CreateSqlStatement_Resolved (type));
      var joinedTable = new SqlTable (joinedTableInfo);

      return new SqlJoin (joinedTable, joinSemantics, new NullJoinConditionExpression());
    }

    private SqlJoin CreateResolvedJoinForJoinedGroupingTableInfoWithoutJoinCondition (Type type, JoinSemantics joinSemantics, string joinedTableAlias)
    {
      var joinedTableInfo = new ResolvedJoinedGroupingTableInfo (
          joinedTableAlias,
          SqlStatementModelObjectMother.CreateSqlStatement_Resolved (type),
          SqlStatementModelObjectMother.CreateSqlGroupingSelectExpression(),
          "gs");
      var joinedTable = new SqlTable (joinedTableInfo);

      return new SqlJoin (joinedTable, joinSemantics, new NullJoinConditionExpression());
    }

    private SqlAppendedTable CreateResolvedAppendedTable (string tableName, string tableAlias, JoinSemantics joinSemantics)
    {
      var sqlTable = SqlStatementModelObjectMother.CreateSqlTable (new ResolvedSimpleTableInfo (typeof (int), tableName, tableAlias));
      return SqlStatementModelObjectMother.CreateSqlAppendedTable (sqlTable, joinSemantics);
    }
  }
}