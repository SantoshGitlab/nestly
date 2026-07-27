import { useRouter } from 'next/router';
import { apiFetch } from '@/lib/api';

export default function DeleteAddressPage() {
  const router = useRouter();
  const { id } = router.query;
  const [error, setError] = useState(null);

  const handleDelete = async () => {
    try {
      await apiFetch(`/api/address/${id}`, {
        method: 'DELETE',
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
      <h1>Delete Address</h1>
      {error && <p>Error: {error}</p>}
      <button onClick={handleDelete}>Delete Address</button>
    </div>
  );
}
