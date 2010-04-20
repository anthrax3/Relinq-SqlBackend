// This file is part of the re-motion Core Framework (www.re-motion.org)
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
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using Remotion.Data.Linq.SqlBackend.MappingResolution;
using Remotion.Data.Linq.SqlBackend.SqlStatementModel.Resolved;
using Remotion.Data.Linq.SqlBackend.SqlStatementModel.Unresolved;
using Remotion.Data.Linq.UnitTests.Linq.Core.TestDomain;
using Remotion.Data.Linq.UnitTests.Linq.SqlBackend.SqlStatementModel;
using Rhino.Mocks;
using System.Linq.Expressions;

namespace Remotion.Data.Linq.UnitTests.Linq.SqlBackend.MappingResolution
{
  [TestFixture]
  public class ResolvingJoinInfoVisitorTest
  {
    private IMappingResolver _resolverMock;
    private UnresolvedJoinInfo _unresolvedJoinInfo;
    private UniqueIdentifierGenerator _generator;
    private IMappingResolutionStage _stageMock;


    [SetUp]
    public void SetUp ()
    {
      _resolverMock = MockRepository.GenerateMock<IMappingResolver>();
      _unresolvedJoinInfo = SqlStatementModelObjectMother.CreateUnresolvedJoinInfo_KitchenCook();
      _generator = new UniqueIdentifierGenerator();
      _stageMock = MockRepository.GenerateMock<IMappingResolutionStage>();
    }

    [Test]
    public void ResolveJoinInfo_ResolvesUnresolvedJoinInfo ()
    {
      var foreignTableInfo = new ResolvedSimpleTableInfo (typeof (string), "Cook", "c");
      var primaryColumn = new SqlColumnExpression (typeof (int), "k", "ID");
      var foreignColumn = new SqlColumnExpression (typeof (int), "c", "KitchenID");

      var resolvedJoinInfo = new ResolvedJoinInfo (foreignTableInfo, primaryColumn, foreignColumn);

      _resolverMock
          .Expect (mock => mock.ResolveJoinInfo (Arg<UnresolvedJoinInfo>.Is.Anything, Arg.Is (_generator)))
          .Return (resolvedJoinInfo);
      _resolverMock.Replay();

      var result = ResolvingJoinInfoVisitor.ResolveJoinInfo (_unresolvedJoinInfo, _resolverMock, _generator, _stageMock);

      Assert.That (result, Is.SameAs (resolvedJoinInfo));
      _resolverMock.VerifyAllExpectations();
    }

    [Test]
    public void ResolveJoinInfo_ResolvesUnresolvedJoinInfo_AndRevisitsResult ()
    {
      // TODO Review 2615: To test that the result is revisited, ResolveJoinInfo must return a different unresolved join info on the first call; then a second call must be expected that returns the final result

      var resolvedJoinInfo = new ResolvedJoinInfo (
          new ResolvedSimpleTableInfo (typeof (Cook), "CookTable", "c"),
          new SqlColumnExpression (typeof (string), "c", "ID"),
          new SqlColumnExpression (typeof (string), "c", "ID"));
      _resolverMock
          .Expect (mock => mock.ResolveJoinInfo (_unresolvedJoinInfo, _generator))
          .Return (resolvedJoinInfo);
      _resolverMock.Replay ();

      var result = ResolvingJoinInfoVisitor.ResolveJoinInfo (_unresolvedJoinInfo, _resolverMock, _generator, _stageMock);

      Assert.That (result, Is.SameAs (resolvedJoinInfo));
      _resolverMock.VerifyAllExpectations ();
    }

    [Test]
    public void ResolveJoinInfo_ResolvesCollectionJoinInfo ()
    {
      // TODO Review 2615: Use a collection-valued property
      var unresolvedCollectionJoinInfo = new UnresolvedCollectionJoinInfo (Expression.Constant (new Cook()), typeof (Cook).GetProperty ("FirstName"));

      var sqlEntityExpression = SqlStatementModelObjectMother.CreateSqlEntityExpression (typeof (Cook));

      var foreignTableInfo = new ResolvedSimpleTableInfo (typeof (string), "Cook", "c");
      var primaryColumn = new SqlColumnExpression (typeof (int), "k", "ID");
      var foreignColumn = new SqlColumnExpression (typeof (int), "c", "KitchenID");
      var expectedResolvedJoinInfo = new ResolvedJoinInfo (foreignTableInfo, primaryColumn, foreignColumn);

      _stageMock
          .Expect (mock => mock.ResolveCollectionSourceExpression (unresolvedCollectionJoinInfo.SourceExpression))
          .Return (sqlEntityExpression);
      _stageMock.Replay();
      
      _resolverMock
          .Expect (mock => mock.ResolveJoinInfo (Arg<UnresolvedJoinInfo>.Is.Anything, Arg.Is (_generator))) // TODO Review 2615: Change to check UnresolvedJoinInfo: should have same member as above, should have sqlEntityExpression.SqlTable
          .Return (expectedResolvedJoinInfo);
      _resolverMock.Replay ();

      var resolvedJoinInfo = ResolvingJoinInfoVisitor.ResolveJoinInfo (unresolvedCollectionJoinInfo, _resolverMock, _generator, _stageMock);

      Assert.That (resolvedJoinInfo, Is.SameAs (expectedResolvedJoinInfo));

      _stageMock.VerifyAllExpectations();
      _resolverMock.VerifyAllExpectations();
    }
  }
}