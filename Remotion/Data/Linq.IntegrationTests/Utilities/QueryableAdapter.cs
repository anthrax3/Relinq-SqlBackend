﻿using System;
using System.Linq;
using System.Linq.Expressions;

namespace Remotion.Data.Linq.IntegrationTests.Utilities
{
  class QueryableAdapter<T> : QueryableBase<T>
  {
    public QueryableAdapter (IQueryExecutor executor)
        : base(executor)
    {
    }

    public QueryableAdapter (IQueryProvider provider, Expression expression)
        : base(provider, expression)
    {
    }


  }
}
