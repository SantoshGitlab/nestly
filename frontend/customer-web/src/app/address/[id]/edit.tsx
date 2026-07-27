import { useRouter } from 'next/router';
import { apiFetch } from '@/lib/api';

export default function EditAddressPage() {
  const router = useRouter();
  const { id } = router.query;
  const [street, setStreet] = useState('');
  const [city, setCity] = useState('');
  const [state, setState] = useState('');
  const [zipCode, setZipCode] = useState('');
  const [error, setError] = useState(null);

  useEffect(() => {
    if (id) {
      fetchAddress(id);
    }
  }, [id]);

  const fetchAddress = async (id) => {
    try {
      const response = await apiFetch(`/api/address/${id}`);
      setStreet(response.data.street);
      setCity(response.data.city);
      setState(response.data.state);
      setZipCode(response.data.zipCode);
    } catch (err) {
      setError(err.message);
    }
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      await apiFetch(`/api/address/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ street, city, state, zipCode }),
      });
      router.push('/address');
    } catch (err) {
      setError(err.message);
    }
  };

  if (error) return <div>Error: {error}</div>;
  if (!street) return <div>Loading...</div>;

  return (
    <div>
      <h1>Edit Address</h1>
      {error && <p>Error: {error}</p>}
      <form onSubmit={handleSubmit}>
        <label>
          Street:
          <input type="text" value={street} onChange={(e) => setStreet(e.target.value)} required />
        </label>
        <br />
        <label>
          City:
          <input type="text" value={city} onChange={(e) => setCity(e.target.value)} required />
        </label>
        <br />
        <label>
          State:
          <input type="text" value={state} onChange={(e) => setState(e.target.value)} required />
        </label>
        <br />
        <label>
          Zip Code:
          <input type="text" value={zipCode} onChange={(e) => setZipCode(e.target.value)} required />
        </label>
        <br />
        <button type="submit">Update Address</button>
      </form>
    </div>
  );
}
