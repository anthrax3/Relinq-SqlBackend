// This file is part of the re-motion Core Framework (www.re-motion.org)
// Copyright (C) 2005-2008 rubicon informationstechnologie gmbh, www.rubicon.eu
// 
// The re-motion Core Framework is free software; you can redistribute it 
// and/or modify it under the terms of the GNU Lesser General Public License 
// version 3.0 as published by the Free Software Foundation.
// 
// re-motion is distributed in the hope that it will be useful, 
// but WITHOUT ANY WARRANTY; without even the implied warranty of 
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the 
// GNU Lesser General Public License for more details.
// 
// You should have received a copy of the GNU Lesser General Public License
// along with re-motion; if not, see http://www.gnu.org/licenses.
// 
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Remotion.Data.Linq.Clauses;
using Remotion.Data.Linq.DataObjectModel;
using Remotion.Data.Linq.Parsing;
using Remotion.Data.Linq.Parsing.Details;
using Remotion.Data.Linq.Parsing.Details.SelectProjectionParsing;
using Remotion.Utilities;

namespace Remotion.Data.Linq.SqlGeneration
{
  public class SqlGeneratorVisitor : IQueryVisitor
  {
    private readonly IDatabaseInfo _databaseInfo;
    private readonly DetailParserRegistries _detailParserRegistries;
    private readonly ParseContext _parseContext;

    private bool _secondOrderByClause;

    public SqlGeneratorVisitor (
        IDatabaseInfo databaseInfo, ParseMode parseMode, DetailParserRegistries detailParserRegistries, ParseContext parseContext)
    {
      ArgumentUtility.CheckNotNull ("databaseInfo", databaseInfo);
      ArgumentUtility.CheckNotNull ("parseContext", parseMode);
      ArgumentUtility.CheckNotNull ("detailParser", detailParserRegistries);
      ArgumentUtility.CheckNotNull ("parseContext", parseContext);

      _databaseInfo = databaseInfo;
      _detailParserRegistries = detailParserRegistries;
      _parseContext = parseContext;

      _secondOrderByClause = false;

      SqlGenerationData = new SqlGenerationData { ParseMode = parseMode };
    }

    public SqlGenerationData SqlGenerationData { get; private set; }

    public void VisitQueryModel (QueryModel queryModel)
    {
      ArgumentUtility.CheckNotNull ("queryExpression", queryModel);
      queryModel.MainFromClause.Accept (this);
      foreach (IBodyClause bodyClause in queryModel.BodyClauses)
        bodyClause.Accept (this);

      queryModel.SelectOrGroupClause.Accept (this);
    }

    public void VisitMainFromClause (MainFromClause fromClause)
    {
      ArgumentUtility.CheckNotNull ("fromClause", fromClause);
      VisitFromClause (fromClause);
    }

    public void VisitAdditionalFromClause (AdditionalFromClause fromClause)
    {
      ArgumentUtility.CheckNotNull ("fromClause", fromClause);
      VisitFromClause (fromClause);
    }

    public void VisitMemberFromClause (MemberFromClause fromClause)
    {
      ArgumentUtility.CheckNotNull ("fromClause", fromClause);
      VisitFromClause (fromClause);

      var memberExpression = fromClause.MemberExpression;
      var leftSide = _detailParserRegistries.WhereConditionParser.GetParser (memberExpression.Expression).Parse (memberExpression.Expression, _parseContext);
      var foreignKeyName = DatabaseInfoUtility.GetJoinColumnNames (_databaseInfo, memberExpression.Member).B;
      var rightSide = new Column (fromClause.GetFromSource (_databaseInfo), foreignKeyName);

      ICriterion criterion = new BinaryCondition (leftSide, rightSide, BinaryCondition.ConditionKind.Equal);
      SqlGenerationData.AddWhereClause (criterion, _parseContext.FieldDescriptors);
    }

    public void VisitSubQueryFromClause (SubQueryFromClause fromClause)
    {
      ArgumentUtility.CheckNotNull ("fromClause", fromClause);
      VisitFromClause (fromClause);
    }

    private void VisitFromClause (FromClauseBase fromClause)
    {
      IColumnSource columnSource = fromClause.GetFromSource (_databaseInfo);

      SqlGenerationData.AddFromClause (columnSource);
    }

    public void VisitJoinClause (JoinClause joinClause)
    {
      throw new NotImplementedException();
    }

    public void VisitWhereClause (WhereClause whereClause)
    {
      ArgumentUtility.CheckNotNull ("whereClause", whereClause);

      LambdaExpression boolExpression = whereClause.GetSimplifiedBoolExpression();
      ICriterion criterion = _detailParserRegistries.WhereConditionParser.GetParser (boolExpression.Body).Parse (boolExpression.Body, _parseContext);

      SqlGenerationData.AddWhereClause (criterion, _parseContext.FieldDescriptors);
    }

    public void VisitOrderByClause (OrderByClause orderByClause)
    {
      ArgumentUtility.CheckNotNull ("orderByClause", orderByClause);

      for (int i = 0; i < orderByClause.OrderingList.Count; i++)
      {
        Ordering clause = orderByClause.OrderingList[i];
        clause.Accept (this);
        if (i == (orderByClause.OrderingList.Count - 1))
          _secondOrderByClause = true;
      }
    }

    public void VisitOrdering (Ordering ordering)
    {
      ArgumentUtility.CheckNotNull ("ordering", ordering);
      var fieldParser = new OrderingFieldParser (_databaseInfo);
      OrderingField orderingField = fieldParser.Parse (ordering.Expression.Body, _parseContext, ordering.OrderingDirection);

      if (!_secondOrderByClause)
        SqlGenerationData.AddOrderingFields (orderingField);
      else
        SqlGenerationData.AddFirstOrderingFields (orderingField);
    }

    public void VisitSelectClause (SelectClause selectClause)
    {
      ArgumentUtility.CheckNotNull ("selectClause", selectClause);
      Expression projectionBody =
          selectClause.ProjectionExpression != null ? selectClause.ProjectionExpression.Body : _parseContext.QueryModel.MainFromClause.Identifier;

      List<MethodCall> methodCalls = GetMethodCalls(selectClause);

      IEvaluation evaluation =
          _detailParserRegistries.SelectProjectionParser.GetParser (projectionBody).Parse (projectionBody, _parseContext);

      SetSelectClause (methodCalls, evaluation);
    }

    private List<MethodCall> GetMethodCalls (SelectClause selectClause)
    {
      List<MethodCall> methodCalls = new List<MethodCall>();
      ResultModifierParser parser = new ResultModifierParser (_detailParserRegistries.SelectProjectionParser);

      if (selectClause.ResultModifierClauses != null)
      {
        foreach (var resultModifierClause in selectClause.ResultModifierClauses)
        {
          methodCalls.Add (parser.Parse (resultModifierClause.ResultModifier, _parseContext));
        }
        
      }
      return methodCalls;
    }

    private void SetSelectClause (List<MethodCall> methodCalls, IEvaluation evaluation)
    {
      if (methodCalls.Count != 0)
        SqlGenerationData.SetSelectClause (methodCalls, _parseContext.FieldDescriptors, evaluation);
      else
        SqlGenerationData.SetSelectClause (null, _parseContext.FieldDescriptors, evaluation);
    }

    public void VisitLetClause (LetClause letClause)
    {
      ArgumentUtility.CheckNotNull ("letClause", letClause);
      Expression projectionBody = letClause.Expression;

      IEvaluation evaluation =
          _detailParserRegistries.SelectProjectionParser.GetParser (projectionBody).Parse (projectionBody, _parseContext);


      LetData letData = new LetData (evaluation, letClause.Identifier.Name, letClause.GetColumnSource (_databaseInfo));
      SqlGenerationData.AddLetClause (letData, _parseContext.FieldDescriptors);
    }

    public void VisitGroupClause (GroupClause groupClause)
    {
      throw new NotImplementedException();
    }
  }
}
