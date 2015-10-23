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
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Remotion.Linq.SqlBackend.MappingResolution;
using Remotion.Linq.SqlBackend.SqlStatementModel.Resolved;
using Remotion.Linq.SqlBackend.SqlStatementModel.Unresolved;
using Remotion.Utilities;

namespace Remotion.Linq.SqlBackend.SqlStatementModel
{
  /// <summary>
  /// <see cref="SqlTable"/> represents a data source in a <see cref="SqlStatement"/>.
  /// </summary>
  public class SqlTable
  {
    public class LeftJoinData
    {
      private readonly SqlTable _joinedTable;
      private readonly Expression _joinCondition;

      public LeftJoinData (SqlTable joinedTable, Expression joinCondition)
      {
        ArgumentUtility.CheckNotNull ("joinedTable", joinedTable);
        ArgumentUtility.CheckNotNull ("joinCondition", joinCondition);

        _joinedTable = joinedTable;
        _joinCondition = joinCondition;
      }

      public SqlTable JoinedTable
      {
        get { return _joinedTable; }
      }

      public Expression JoinCondition
      {
        get { return _joinCondition; }
      }
    }

    private struct SortableSqlJoin
    {
      public readonly int Index;
      public readonly SqlJoin Value;

      public SortableSqlJoin (int index, SqlJoin value)
      {
        Index = index;
        Value = value;
      }
    }

    private readonly List<SqlJoin> _orderedJoinsForExplicitQuerySources = new List<SqlJoin>();
    private readonly Dictionary<MemberInfo, SortableSqlJoin> _memberBasedJoinsByMemberInfo = new Dictionary<MemberInfo, SortableSqlJoin>();

    private ITableInfo _tableInfo;

    public SqlTable (ITableInfo tableInfo)
    {
      ArgumentUtility.CheckNotNull ("tableInfo", tableInfo);

      _tableInfo = tableInfo;
    }

    /// <summary>
    /// Gets a list of all joins added via <see cref="GetOrAddMemberBasedLeftJoin"/> and <see cref="AddJoinForExplicitQuerySource"/>. 
    /// Both sets of joins are orderd by their order of insertion and then the sets are concatenated.
    /// </summary>
    public IEnumerable<SqlJoin> Joins
    {
      get { return _memberBasedJoinsByMemberInfo.Values.OrderBy (j => j.Index).Select (j => j.Value).Concat (_orderedJoinsForExplicitQuerySources); }
    }


    public Type ItemType
    {
      get { return _tableInfo.ItemType; }
    }

    /// <remarks>The property is currently mutable because of a missing refactoring. It could be made immutable by using the 
    /// <see cref="IMappingResolutionContext"/> to map <see cref="SqlTableReferenceExpression"/> instances pointing to old <see cref="SqlTable"/>
    /// objects to those pointing to the new <see cref="SqlTable"/> instances.</remarks>
    public ITableInfo TableInfo
    {
      get { return _tableInfo; }
      set
      {
        Assertion.IsNotNull (_tableInfo);
        try
        {
          ArgumentUtility.CheckNotNull ("value", value);

          if (ItemType != value.ItemType)
            throw ArgumentUtility.CreateArgumentTypeException ("value", value.ItemType, _tableInfo.ItemType);

          _tableInfo = value;
        }
        finally
        {
          Assertion.IsNotNull (_tableInfo);
        }
      }
    }

    public IResolvedTableInfo GetResolvedTableInfo ()
    {
      return TableInfo.GetResolvedTableInfo();
    }

    /// <summary>
    /// Adds a join representing a member access to this <see cref="SqlTable"/> or returns it if such a join has already been added for this member.
    /// Note that SQL requires that the right side of a join must not reference the left side of a join in SQL (apart from in the join condition). 
    /// For cases where this doesn't hold, add the joined table via <see cref="SqlStatement.SqlTables"/> instead and put the join condition into a WHERE condition. 
    /// (Note that for LEFT joins, the join condition must be embedded within the applied table; i.e., a sub-statement must be used.)
    /// </summary>
    public SqlJoin GetOrAddMemberBasedLeftJoin (MemberInfo memberInfo, Func<LeftJoinData> joinDataFactory)
    {
      ArgumentUtility.CheckNotNull ("memberInfo", memberInfo);
      ArgumentUtility.CheckNotNull ("joinDataFactory", joinDataFactory);

      SortableSqlJoin sqlJoin;
      if (!_memberBasedJoinsByMemberInfo.TryGetValue (memberInfo, out sqlJoin))
      {
        var joinData = joinDataFactory();
        sqlJoin = new SortableSqlJoin (
            _memberBasedJoinsByMemberInfo.Count,
            new SqlJoin (joinData.JoinedTable, JoinSemantics.Left, joinData.JoinCondition));
        _memberBasedJoinsByMemberInfo.Add (memberInfo, sqlJoin);
      }

      return sqlJoin.Value;
    }

    /// <summary>
    /// Adds a join to this <see cref="SqlTable"/>. Note that SQL requires that the right side of a join must not reference the left side of a join 
    /// in SQL (apart from in the join condition). For cases where this doesn't hold, add the joined table via <see cref="SqlStatement.SqlTables"/>
    /// instead and put the join condition into a WHERE condition. (Note that for LEFT joins, the join condition must be embedded within the applied
    /// table; i.e., a sub-statement must be used.)
    /// </summary>
    public void AddJoinForExplicitQuerySource (SqlJoin sqlJoin)
    {
      ArgumentUtility.CheckNotNull ("sqlJoin", sqlJoin);
      _orderedJoinsForExplicitQuerySources.Add (sqlJoin);
    }

    public SqlJoin GetJoinByMember (MemberInfo relationMember)
    {
      ArgumentUtility.CheckNotNull ("relationMember", relationMember);

      return _memberBasedJoinsByMemberInfo[relationMember].Value;
    }

    // TODO RMLNQSQL-7: This method is only required because we want to keep SqlJoin immutable. Maybe refactor it toward SqlJoinBuilder (mutable) and 
    // SqlJoin (immutable) later on. This would fit well with a SqlTableBuilder (mutable) and a SqlTable (immutable).
    public void SubstituteJoins (IDictionary<SqlJoin, SqlJoin> substitutions)
    {
      ArgumentUtility.CheckNotNull ("substitutions", substitutions);

      for (int i = 0; i < _orderedJoinsForExplicitQuerySources.Count; ++i)
      {
        SqlJoin substitution;
        if (substitutions.TryGetValue (_orderedJoinsForExplicitQuerySources[i], out substitution))
          _orderedJoinsForExplicitQuerySources[i] = substitution;
      }

      foreach (var kvp in _memberBasedJoinsByMemberInfo.ToArray())
      {
        MemberInfo memberInfo = kvp.Key;
        SqlJoin original = kvp.Value.Value;
        SqlJoin substitution;
        if (substitutions.TryGetValue (original, out substitution))
          _memberBasedJoinsByMemberInfo[memberInfo] = new SortableSqlJoin (kvp.Value.Index, substitution);
      }
    }

    public override string ToString ()
    {
      var sb = new StringBuilder();
      sb.Append (TableInfo);
      AppendJoinString (sb, Joins);

      return sb.ToString();
    }

    private void AppendJoinString (StringBuilder sb, IEnumerable<SqlJoin> orderedJoins)
    {
      foreach (var sqlJoin in orderedJoins)
      {
        sb
            .Append (" ")
            .Append (sqlJoin.JoinSemantics.ToString().ToUpper())
            .Append (" JOIN ")
            .Append (sqlJoin.JoinedTable.TableInfo)
            .Append (" ON ")
            .Append (sqlJoin.JoinCondition);
        AppendJoinString (sb, sqlJoin.JoinedTable.Joins);
      }
    }
  }
}