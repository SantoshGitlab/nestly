using System.Collections.Generic;

namespace Nestly.Domain;

public interface IOrderingStrategy<T>
{
    IEnumerable<T> Order(IEnumerable<T> items);
}
