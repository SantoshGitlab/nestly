import { useEffect, useState } from 'react';
import { apiFetch } from '@/lib/api';

export default function AddressListPage() {
  const [addresses, setAddresses] = useState([]);
  const [error, setError] = useState(null);

  useEffect(() => {
    fetchAddresses();
  }, []);

  const fetchAddresses = async () => {
    try {
      const response = await apiFetch('/api/address');
      setAddresses(response.data);
    } catch (err) {
      setError(err.message);
    }
  };

  if (error) return <div>Error: {error}</div>;
  if (!addresses.length) return <div>No addresses found.</div>;

  return (
    <div>
      <h1>Address List</h1>
      <ul>
        {addresses.map((address) => (
          <li key={address.id}>
            {address.street}, {address.city}, {address.state} {address.zipCode}
            <a href={`/address/${address.id}`}>View Details</a>
          </li>
        ))}
      </ul>
    </div>
  );
}
