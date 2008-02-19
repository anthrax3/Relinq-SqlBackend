using System.Collections.Generic;
using Rubicon.Data.Linq.DataObjectModel;

namespace Rubicon.Data.Linq.SqlGeneration
{
  public interface IFromBuilder
  {
    void BuildFromPart (List<Table> tables, JoinCollection joins);
  }
}