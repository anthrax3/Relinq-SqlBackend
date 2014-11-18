﻿// This file is part of the re-linq project (relinq.codeplex.com)
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
using Remotion.Linq.Parsing.Structure;

namespace Remotion.Linq.LinqToSqlAdapter
{
  /// <summary>
  /// Default implementation of <see cref="QueryableBase{T}"/> 
  /// </summary>
  public class RelinqQueryable<T> : QueryableBase<T>
  {
    public RelinqQueryable (IQueryParser queryParser, IQueryExecutor executor)
        : base (new DefaultQueryProvider (typeof (RelinqQueryable<>), queryParser, executor))
    {
    }

    public RelinqQueryable (IQueryProvider provider, Expression expression)
        : base (provider, expression)
    {
    }
  }
}