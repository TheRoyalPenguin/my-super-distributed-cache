import { useEffect, useState } from 'react';

function CircleNodes() {
  const [nodes, setNodes] = useState([]);
  const [selectedNode, setSelectedNode] = useState(null);
  const [error, setError] = useState(null);
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    fetchNodes();
  }, []);

  async function fetchNodes() {
    setIsLoading(true);
    setError(null);
    setNodes([]);

    try {
      const res = await fetch('/api/monitor/nodes');
      if (res.status === 404) {
        setError('Список узлов пуст!');
        return;
      }
      if (!res.ok) throw new Error(`Ошибка ${res.status}`);
      const data = await res.json();
      setNodes(data);
    } catch (err) {
      setError(`Ошибка: ${err.message}`);
    } finally {
      setIsLoading(false);
  }
  }

  function renderNodeDetails(node, depth = 0) {
    return (
      <div className={depth > 0 ? 'replica' : ''} key={node.id}>
        <h2>{node.name}</h2>
        <p><strong>Статус:</strong> {getNodeStatus(node.status)}{node.status}</p>
        <p><strong>ID:</strong> {node.id}</p>
        <p><strong>URL:</strong> {node.url}</p>
        <div>
          <strong>Кэш:</strong> {node.items.length === 0 ? ("пусто") : ("")}
          <ul>
            {node.items.map((item, i) => (
              <li key={i}>
                <div><strong>Ключ:</strong> {item.key}</div>
                <div><strong>Значение:</strong> {JSON.stringify(item.value)}</div>
                <div><strong>Создан:</strong> {formatDate(item.createdAt)}</div>
                <div><strong>Последний доступ:</strong> {formatDate(item.lastAccessed)}</div>
                {item.ttl && <div><strong>TTL:</strong> {item.ttl}</div>}
              </li>
            ))}
          </ul>
        </div>
        {node.replicas?.length > 0 && (
          <div><strong>Реплики:</strong>{node.replicas.map(r => renderNodeDetails(r, depth + 1))}</div>
        )}
      </div>
    );
  }

    function formatDate(isoString) {
    if (!isoString) return '-';
    const date = new Date(isoString);
    return date.toLocaleString('ru-RU', {
      year: 'numeric',
      month: 'numeric',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
    });
  }

  const getNodeStatus = (status) => {
    switch (status) {
      case 'Online': return '🟢';
      case 'Offline': return '⚫️';
      case 'Initializing': return '🟠';
      case 'Error': return '🔴';
      default: return '❓';
    }
  };

  const centerX = 300;
  const centerY = 300;
  const total = nodes.length;
  const baseRadius = 250;
  const radius = baseRadius + Math.max(0, total - 12) * 10;

  return (
    <>
      <div className="circle-container">
        {error && <p style={{ color: 'red' }}>{error}</p>}

        <button
          className="refresh-button"
          disabled={isLoading}
          onClick={fetchNodes}
          style={{
            position: 'absolute',
            left: `${centerX}px`,
            top: `${centerY}px`,
            transform: 'translate(-50%, -50%)',
            zIndex: 2,
            padding: '10px 20px',
            borderRadius: '8px',
            background: isLoading ? '#ccc' : '#007bff',
            color: isLoading ? '#666' : 'white',
            cursor: isLoading ? 'not-allowed' : 'pointer',
            border: 'none',
            boxShadow: '0 2px 6px rgba(0, 0, 0, 0.2)',
          }}
        >
        {isLoading ? '⏳ Обновляется...' : '🔄 Обновить'}</button>
        {nodes.map((node, index) => {
          const angle = (2 * Math.PI / total) * index - Math.PI / 2;
          const x = centerX + radius * Math.cos(angle);
          const y = centerY + radius * Math.sin(angle);

          return (
            <div
              className="node"
              key={node.id}
              style={{ left: `${x}px`, top: `${y}px` }}
              onClick={() => setSelectedNode(node)}
            >
              <p>{getNodeStatus(node.status)} Статус: {node.status}</p>
              <h3>{node.name}</h3>
              <small>ID: {node.id.slice(0, 6)}...</small><br />
              <small>URL: {node.url}</small>
            </div>
          );
        })}
      </div>

      {selectedNode && (
        <div className="modal-overlay" onClick={() => setSelectedNode(null)}>
          <div className="modal" onClick={e => e.stopPropagation()}>
            <span className="close-btn" onClick={() => setSelectedNode(null)}>&times;</span>
            <div>{renderNodeDetails(selectedNode)}</div>
          </div>
        </div>
      )}
    </>
  );
}

export default CircleNodes;
