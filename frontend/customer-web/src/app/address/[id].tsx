import { useEffect, useState } from 'react';
import { useRouter } from 'next/router';
import { apiFetch } from '@/lib/api';

export default function AddressDetailPage() {
  const router = useRouter();
  const { id } = router.query;
  const [address, setAddress] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (id) {
      fetchAddress(id);
    }
  }, [id]);

  const fetchAddress = async (id) => {
    try {
      const response = await apiFetch(`/api/address/${id}`);
      setAddress(response.data);
    } catch (err) {
      setError(err.message);
    }
  };

  const handleEdit = async () => {
    // Implement edit logic here
  };

  if (error) return <div>Error: {error}</div>;
  if (!address) return <div>Loading...</div>;

  return (
    <div>
      <h1>Address Detail</h1>
      <p>Street: {address.street}</p>
      <p>City: {address.city}</p>
      <p>State: {address.state}</p>
      <p>Zip Code: {address.zipCode}</p>
      <button onClick={handleEdit}>Edit Address</button>
    </div>
  );
}
