﻿// This file is part of the re-motion Core Framework (www.re-motion.org)
// Copyright (C) 2005-2009 rubicon informationstechnologie gmbh, www.rubicon.eu
// 
// The re-motion Core Framework is free software; you can redistribute it 
// and/or modify it under the terms of the GNU Lesser General Public License 
// as published by the Free Software Foundation; either version 2.1 of the 
// License, or (at your option) any later version.
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
using System.Data.Linq;
using System.Data.Linq.Mapping;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using Remotion.Data.Linq.IntegrationTests.TestDomain.Northwind;
using Remotion.Data.Linq.IntegrationTests.Utilities;
using Remotion.Data.Linq.SqlBackend.SqlStatementModel;
using Remotion.Data.Linq.SqlBackend.SqlStatementModel.Resolved;
using Remotion.Data.Linq.SqlBackend.SqlStatementModel.Unresolved;

namespace Remotion.Data.Linq.IntegrationTests.UnitTests
{
  [TestFixture]
  public class MappingResolverTests
  {
    private UniqueIdentifierGenerator _generator;
    private NorthwindMappingResolver _mappingResolver;

    [SetUp]
    public void SetUp()
    {
       _generator=new UniqueIdentifierGenerator();
       _mappingResolver = new NorthwindMappingResolver ();
    }

    [Test]
    public void TestMetaModelMapping()
    {
      MappingSource mappingSource = new AttributeMappingSource();

      var table = mappingSource.GetModel (typeof (Northwind)).GetTable (typeof (Customer));
      Assert.AreEqual ("dbo.Customers",table.TableName);

      string companyName = "CompanyName";

      string expectedType = "NVarChar(40) NOT NULL";
      string resolvedType=string.Empty;
      
      foreach (var metaDataMember in table.RowType.DataMembers)
      {
        if(!metaDataMember.Name.Equals (companyName))
          continue;

        resolvedType = metaDataMember.DbType;
      }

      Assert.AreEqual (expectedType, resolvedType);
    }

    [Test]
    public void TestResolveTableInfo()
    {
      UnresolvedTableInfo unresolvedTableInfo = new UnresolvedTableInfo (typeof(Customer));
      
      ResolvedSimpleTableInfo resolvedTableInfo = (ResolvedSimpleTableInfo) _mappingResolver.ResolveTableInfo (unresolvedTableInfo, _generator);

      ResolvedSimpleTableInfo simpleTableInfo=new ResolvedSimpleTableInfo (typeof(Customer),"dbo.Customers","t0");
      
      Assert.AreEqual (simpleTableInfo.ItemType, resolvedTableInfo.ItemType);
      Assert.AreEqual (simpleTableInfo.TableAlias, resolvedTableInfo.TableAlias);
      Assert.AreEqual (simpleTableInfo.TableName, resolvedTableInfo.TableName);
    }

    [Test]
    public void TestResolveSimpleTableInfo()
    {
      ResolvedSimpleTableInfo simpleTableInfo = new ResolvedSimpleTableInfo (typeof (Region), "dbo.Region", "t0");

      SqlColumnExpression primaryColumn = new SqlColumnDefinitionExpression(typeof(int), simpleTableInfo.TableAlias, "RegionID", true);
      SqlColumnExpression descriptionColumn = new SqlColumnDefinitionExpression (
          typeof (string), simpleTableInfo.TableAlias, "RegionDescription", false);
      SqlColumnExpression territoriesColumn = new SqlColumnDefinitionExpression (
          typeof (EntitySet<Territory>), simpleTableInfo.TableAlias, "Region_Territory", false);

      SqlEntityDefinitionExpression expectedExpr = new SqlEntityDefinitionExpression (
          simpleTableInfo.ItemType, simpleTableInfo.TableAlias, null, primaryColumn, descriptionColumn, territoriesColumn);

      SqlEntityDefinitionExpression resolvedExpr = _mappingResolver.ResolveSimpleTableInfo (simpleTableInfo, _generator);

      ExpressionTreeComparer.CheckAreEqualTrees (expectedExpr, resolvedExpr);
    }

    [Test]
    public void TestResolveJoinInfo ()
    {
      ResolvedSimpleTableInfo orderTableInfo = new ResolvedSimpleTableInfo (typeof (Order), "dbo.Order", "t0");
      ResolvedSimpleTableInfo customerTableInfo = new ResolvedSimpleTableInfo (typeof (Customer), "dbo.Customers", "t1");

      SqlColumnDefinitionExpression customerPrimaryKey = new SqlColumnDefinitionExpression (
          typeof (string), customerTableInfo.TableAlias, "CustomerID", true);
      SqlColumnDefinitionExpression orderForeignKey = new SqlColumnDefinitionExpression (
          typeof (string), orderTableInfo.TableAlias, "CustomerID", false);

      SqlEntityDefinitionExpression customerDefinition = new SqlEntityDefinitionExpression (
          customerTableInfo.ItemType, customerTableInfo.TableAlias, null, customerPrimaryKey);
      PropertyInfo customerOrders = customerTableInfo.ItemType.GetProperty ("Orders");
      UnresolvedJoinInfo joinInfo = new UnresolvedJoinInfo (customerDefinition, customerOrders, JoinCardinality.Many);

      ResolvedJoinInfo expectedJoinInfo = new ResolvedJoinInfo (orderTableInfo, customerPrimaryKey, orderForeignKey);
      ResolvedJoinInfo resolvedJoinInfo = _mappingResolver.ResolveJoinInfo (joinInfo, _generator);

      ExpressionTreeComparer.CheckAreEqualTrees (expectedJoinInfo.LeftKey, resolvedJoinInfo.LeftKey);
      ExpressionTreeComparer.CheckAreEqualTrees (expectedJoinInfo.RightKey, resolvedJoinInfo.RightKey);
      Assert.AreEqual (expectedJoinInfo.ItemType, resolvedJoinInfo.ItemType);
      Assert.AreEqual (expectedJoinInfo.ForeignTableInfo.ItemType, resolvedJoinInfo.ForeignTableInfo.ItemType);
      Assert.AreEqual (expectedJoinInfo.ForeignTableInfo.TableAlias, resolvedJoinInfo.ForeignTableInfo.TableAlias);
    }

    [Test]
    public void TestResolveJoinInfoReverse ()
    {
      ResolvedSimpleTableInfo customerTableInfo = new ResolvedSimpleTableInfo (typeof (Customer), "dbo.Customers", "t0");
      ResolvedSimpleTableInfo orderTableInfo = new ResolvedSimpleTableInfo (typeof (Order), "dbo.Order", "t1");

      SqlColumnDefinitionExpression customerPrimaryKey = new SqlColumnDefinitionExpression (
          typeof (string), customerTableInfo.TableAlias, "CustomerID", true);
      SqlColumnDefinitionExpression orderForeignKey = new SqlColumnDefinitionExpression (
          typeof (string), orderTableInfo.TableAlias, "CustomerID", false);
      SqlColumnDefinitionExpression orderPrimaryKey = new SqlColumnDefinitionExpression (
          typeof (string), orderTableInfo.TableAlias, "OrderID", true);

      SqlEntityDefinitionExpression orderDefinition = new SqlEntityDefinitionExpression (
          orderTableInfo.ItemType, orderTableInfo.TableAlias, null, orderPrimaryKey);
      PropertyInfo orderCustomer = orderTableInfo.ItemType.GetProperty ("Customer");

      UnresolvedJoinInfo joinInfo = new UnresolvedJoinInfo (orderDefinition, orderCustomer, JoinCardinality.One);

      ResolvedJoinInfo expectedJoinInfo = new ResolvedJoinInfo (customerTableInfo, orderForeignKey, customerPrimaryKey);
      ResolvedJoinInfo resolvedJoinInfo = _mappingResolver.ResolveJoinInfo (joinInfo, _generator);

      ExpressionTreeComparer.CheckAreEqualTrees (expectedJoinInfo.LeftKey, resolvedJoinInfo.LeftKey);
      ExpressionTreeComparer.CheckAreEqualTrees (expectedJoinInfo.RightKey, resolvedJoinInfo.RightKey);
      Assert.AreEqual (expectedJoinInfo.ItemType, resolvedJoinInfo.ItemType);
      Assert.AreEqual (expectedJoinInfo.ForeignTableInfo.ItemType, resolvedJoinInfo.ForeignTableInfo.ItemType);
      Assert.AreEqual (expectedJoinInfo.ForeignTableInfo.TableAlias, resolvedJoinInfo.ForeignTableInfo.TableAlias);
    }

    [Test]
    public void TestReverseMapping ()
    {
      ResolvedSimpleTableInfo simpleTableInfo = new ResolvedSimpleTableInfo (typeof (Region), "dbo.Region", "t0");

      SqlEntityDefinitionExpression resolvedExpr = _mappingResolver.ResolveSimpleTableInfo (simpleTableInfo, _generator);
      MetaDataMember[] metaDataMembers = _mappingResolver.GetMetaDataMembers (simpleTableInfo.ItemType);

      Assert.AreEqual (metaDataMembers[0].MappedName, resolvedExpr.PrimaryKeyColumn.ColumnName);

      for (int i = 1; i < metaDataMembers.Length; i++)
        Assert.AreEqual (metaDataMembers[i].MappedName, resolvedExpr.Columns[i - 1].ColumnName);
    }

    [Test]
    public void  ResolveMemberExpression()
    {
      var primaryKeyColumn = new SqlColumnDefinitionExpression (typeof (string), "p", "First", true);
      var sqlEntityExpression = new SqlEntityDefinitionExpression (typeof (Person), "p", null, primaryKeyColumn);

      var memberInfo = typeof (Person).GetProperty ("First");
      Expression result = _mappingResolver.ResolveMemberExpression (sqlEntityExpression, memberInfo);

      ExpressionTreeComparer.CheckAreEqualTrees (primaryKeyColumn, result);
    }

    [Test]
    public void ResolveMemberExpressionUsingNorthwindEntitiesPrimaryKey ()
    {
      //Test object
      Type type = typeof (Customer);
      string columnName = "CustomerID";
      bool isPrimaryKey = true;

      //Expressions
      var primaryKeyColumn = new SqlColumnDefinitionExpression (typeof (string), "c", columnName, isPrimaryKey);
      var sqlEntityExpression = new SqlEntityDefinitionExpression (type, "c", null, primaryKeyColumn);

      var memberInfo = type.GetProperty (columnName);
      Expression result = _mappingResolver.ResolveMemberExpression (sqlEntityExpression, memberInfo);

      ExpressionTreeComparer.CheckAreEqualTrees (primaryKeyColumn, result);
    }

    [Test]
    public void ResolveMemberExpressionUsingNorthwindEntitiesNonPrimaryKey ()
    {
      //Test object
      Type type = typeof (Customer);
      string columnName = "CompanyName";
      bool isPrimaryKey = false;

      //Expressions
      var primaryKeyColumn = new SqlColumnDefinitionExpression (typeof (string), "c", columnName, isPrimaryKey);
      var sqlEntityExpression = new SqlEntityDefinitionExpression (type, "c", null, primaryKeyColumn);

      var memberInfo = type.GetProperty (columnName);
      Expression result = _mappingResolver.ResolveMemberExpression (sqlEntityExpression, memberInfo);

      ExpressionTreeComparer.CheckAreEqualTrees (primaryKeyColumn, result);
    }

    [Test]
    public void ResolveMemberExpressionUsingNorthwindEntitiesAssociated ()
    {
      var primaryKeyColumn = new SqlColumnDefinitionExpression (typeof (string), "c", "CustomerID", true);
      var referencedSqlException = new SqlEntityDefinitionExpression (typeof (Customer), "c", null, primaryKeyColumn);

      var sqlEntityExpression = new SqlEntityReferenceExpression (typeof (Order), "o", null, referencedSqlException);

      var memberInfo = typeof (Order).GetProperty ("Customer");
      var result = _mappingResolver.ResolveMemberExpression (sqlEntityExpression, memberInfo);

      var expectedExpression = new SqlEntityRefMemberExpression (sqlEntityExpression, memberInfo);

      ExpressionTreeComparer.CheckAreEqualTrees (expectedExpression, result);
    }


  }
}