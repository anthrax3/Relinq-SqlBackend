using System.Collections.Generic;
using System.Reflection;
using Rubicon.Collections;
using Rubicon.Data.Linq.Clauses;
using Rubicon.Data.Linq.DataObjectModel;
using Rubicon.Utilities;
using System.Linq;

namespace Rubicon.Data.Linq.Parsing.FieldResolving
{
  public class WhereFieldAccessPolicy : IResolveFieldAccessPolicy
  {
    private readonly IDatabaseInfo _databaseInfo;

    public WhereFieldAccessPolicy (IDatabaseInfo databaseInfo)
    {
      ArgumentUtility.CheckNotNull ("databaseInfo", databaseInfo);
      _databaseInfo = databaseInfo;
    }

    public Tuple<MemberInfo, IEnumerable<MemberInfo>> AdjustMemberInfosForFromIdentifier (FromClauseBase accessedFromClause)
    {
      ArgumentUtility.CheckNotNull ("accessedFromClause", accessedFromClause);
      return new Tuple<MemberInfo, IEnumerable<MemberInfo>> (_databaseInfo.GetPrimaryKeyMember (accessedFromClause.Identifier.Type), 
          new MemberInfo[0]);
    }

    public Tuple<MemberInfo, IEnumerable<MemberInfo>> AdjustMemberInfosForRelation (MemberInfo accessedMember, IEnumerable<MemberInfo> joinMembers)
    {
      ArgumentUtility.CheckNotNull ("accessedMember", accessedMember);
      ArgumentUtility.CheckNotNull ("joinMembers", joinMembers);
      if (DatabaseInfoUtility.IsVirtualColumn (_databaseInfo, accessedMember))
      {
        MemberInfo primaryKeyMember = DatabaseInfoUtility.GetPrimaryKeyMember (_databaseInfo, ReflectionUtility.GetFieldOrPropertyType (accessedMember));
        return new Tuple<MemberInfo, IEnumerable<MemberInfo>> (primaryKeyMember, joinMembers.Concat (new[] {accessedMember}));
      }
      else
        return new Tuple<MemberInfo, IEnumerable<MemberInfo>> (accessedMember, joinMembers);
    }
  }
}