using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.SyntaxHelpers;
using Rubicon.Data.DomainObjects.Mapping;
using Rubicon.Data.DomainObjects.UnitTests;
using Rubicon.Data.DomainObjects.UnitTests.TestDomain;
using Rubicon.Data.Linq;
using System.Linq;
using Rubicon.Data.Linq.Parsing.Structure;

namespace Rubicon.Data.DomainObjects.Linq.UnitTests
{
  [TestFixture]
  public class QueryExecutorTest : ClientTransactionBaseTest
  {
    private TestQueryListener _listener;

    public override void SetUp ()
    {
      base.SetUp ();
      _listener = new TestQueryListener ();
    }

    [Test]
    public void QueryExecutor_Listener ()
    {
      QueryExecutor<Computer> executor = new QueryExecutor<Computer> (_listener);
      Assert.AreSame (_listener, executor.Listener);
    }

    [Test]
    public void ExecuteSingle()
    {
      SetDatabaseModifyable();

      Computer.GetObject (DomainObjectIDs.Computer1).Delete ();
      Computer.GetObject (DomainObjectIDs.Computer2).Delete ();
      Computer.GetObject (DomainObjectIDs.Computer3).Delete ();
      Computer.GetObject (DomainObjectIDs.Computer4).Delete ();

      ClientTransaction.Current.Commit();
      
      QueryExecutor<Computer> executor = new QueryExecutor<Computer> (_listener);
      QueryExpression expression = GetParsedSimpleQuery ();

      object instance = executor.ExecuteSingle (expression);
      Assert.IsNotNull (instance);
      Assert.AreSame (Computer.GetObject (DomainObjectIDs.Computer5), instance);
    }

    [Test]
    [ExpectedException (ExpectedMessage = "ExecuteSingle must return a single object, but the query returned 5 objects.")]
    public void ExecuteSingle_TooManyObjects()
    {
      QueryExecutor<Computer> executor = new QueryExecutor<Computer> (_listener);
      executor.ExecuteSingle (GetParsedSimpleQuery());
    }

    [Test]
    [ExpectedException (typeof (InvalidOperationException), ExpectedMessage = "No ClientTransaction has been associated with the current thread.")]
    public void QueryExecutor_ExecuteSingle_NoCurrentTransaction ()
    {
      QueryExecutor<Computer> executor = new QueryExecutor<Computer> (null);
      QueryExpression expression = GetParsedSimpleQuery ();

      using (ClientTransactionScope.EnterNullScope ())
      {
        executor.ExecuteSingle (expression);
      }
    }

    [Test]
    public void ExecuteCollection ()
    {
      QueryExecutor<Computer> executor = new QueryExecutor<Computer> (_listener);
      QueryExpression expression = GetParsedSimpleQuery();

      IEnumerable computers = executor.ExecuteCollection (expression);

      ArrayList computerList = new ArrayList();
      foreach (Computer computer in computers)
        computerList.Add (computer);

      Computer[] expected = new Computer[]
          {
              Computer.GetObject (DomainObjectIDs.Computer1),
              Computer.GetObject (DomainObjectIDs.Computer2),
              Computer.GetObject (DomainObjectIDs.Computer3),
              Computer.GetObject (DomainObjectIDs.Computer4),
              Computer.GetObject (DomainObjectIDs.Computer5),
          };
      Assert.That (computerList, Is.EquivalentTo (expected));
    }

    [Test]
    [ExpectedException (typeof (MappingException), ExpectedMessage = "Mapping does not contain class 'System.String'.")]
    public void ExecuteCollection_WrongType ()
    {
      QueryExecutor<string> executor = new QueryExecutor<string> (_listener);
      executor.ExecuteCollection (GetParsedSimpleQuery());
    }

    [Test]
    [ExpectedException (typeof (InvalidOperationException), ExpectedMessage = "No ClientTransaction has been associated with the current thread.")]
    public void QueryExecutor_ExecuteCollection_NoCurrentTransaction ()
    {
      QueryExecutor<Computer> executor = new QueryExecutor<Computer> (null);
      QueryExpression expression = GetParsedSimpleQuery ();

      using (ClientTransactionScope.EnterNullScope ())
      {
        executor.ExecuteCollection (expression);
      }
    }

    [Test]
    public void ExecuteSingle_WithParameters ()
    {
      QueryExecutor<Order> executor = new QueryExecutor<Order> (_listener);
      QueryExpression expression = GetParsedSimpleWhereQuery ();

      Order order = (Order) executor.ExecuteSingle (expression);

      Order expected = Order.GetObject (DomainObjectIDs.Order1);
      Assert.AreSame (expected, order);
    }

    [Test]
    public void ExecuteCollection_WithParameters()
    {
      QueryExecutor<Order> executor = new QueryExecutor<Order> (_listener);
      QueryExpression expression = GetParsedSimpleWhereQuery ();

      IEnumerable orders = executor.ExecuteCollection (expression);

      ArrayList orderList = new ArrayList ();
      foreach (Order order in orders) 
        orderList.Add (order);

      Order[] expected = new Order[]
          {
              Order.GetObject (DomainObjectIDs.Order1),

          };
      Assert.That (orderList, Is.EquivalentTo (expected));
    }

    private QueryExpression GetParsedSimpleQuery ()
    {
      IQueryable<Computer> query = from computer in DataContext.Entity<Computer> () select computer;
      return new QueryParser (query.Expression).GetParsedQuery ();
    }

    private QueryExpression GetParsedSimpleWhereQuery ()
    {
      IQueryable<Order> query = from order in DataContext.Entity<Order> () where order.OrderNumber == 1 select order;
      return new QueryParser (query.Expression).GetParsedQuery ();
    }

  }
}