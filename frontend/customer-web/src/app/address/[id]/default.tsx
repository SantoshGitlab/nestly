import { useRouter } from 'next/router';
import { apiFetch } from '@/lib/api';

export default function SetDefaultAddressPage() {
  const router = useRouter();
  const { id } = router.query;
  const [error, setError] = useState(null);

  const handleSetDefault = async () => {
    try {
      await apiFetch(`/api/address/${id}/default`, {
        method: 'POST',
      });
      router.push('/address');
    } catch (err) {
      setError(err.message);
    }
  };

  if (error) return <div>Error: {error}</div>;
  if (!id) return <div>Loading...</div>;

  return (
    <div>
      <h1>Set Default Address</h1>
      {error && <p>Error: {error}</p>}
      <button onClick={handleSetDefault}>Set as Default Address</button>
    </div>
  );
}
