import { useRouter } from 'next/router';
import { apiFetch } from '@/lib/api';

export default function NewAddressPage() {
  const router = useRouter();
  const [street, setStreet] = useState('');
  const [city, setCity] = useState('');
  const [state, setState] = useState('');
  const [zipCode, setZipCode] = useState('');
  const [error, setError] = useState(null);

  const handleSubmit = async (e) => {
    e.preventDefault();
    try {
      await apiFetch('/api/address', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ street, city, state, zipCode }),
      });
      router.push('/address');
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div>
      <h1>Create New Address</h1>
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
        <button type="submit">Create Address</button>
      </form>
    </div>
  );
}
