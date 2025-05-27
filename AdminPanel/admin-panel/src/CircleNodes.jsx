import { useEffect, useState, useRef } from "react";
import isEqual from "lodash.isequal";

function CircleNodes() {
  const [nodes, setNodes] = useState([]);
  const [selectedNode, setSelectedNode] = useState(null);
  const [error, setError] = useState(null);
  const [isLoading, setIsLoading] = useState(false);
  const [buttonUpdateNodesText, setButtonUpdateText] = useState("🔄 Обновить");
  const nodesRef = useRef(nodes);

  const [isAddOpen, setIsAddOpen] = useState(false);
  const [containerName, setContainerName] = useState("");
  const [copies, setCopies] = useState(1);

  useEffect(() => {
    nodesRef.current = nodes;
  }, [nodes]);

  useEffect(() => {
    let isMounted = true;

    async function checkUpdateNodes() {
      setError(null);
      try {
        const res = await fetch("/api/monitor/nodes");
        if (res.status === 404) {
          return;
        }
        if (!res.ok) throw new Error(`Ошибка ${res.status}`);
        const data = await res.json();
        if (
          data.length !== nodesRef.current.length ||
          !isEqual(data, nodesRef.current)
        ) {
          setButtonUpdateText("Доступно обновление!");
        }
      } catch (err) {
        setError(`Ошибка: ${err.message}`);
      }
    }

    async function runFetchNodesPeriodically() {
      while (isMounted) {
        await checkUpdateNodes();
        await new Promise((resolve) => setTimeout(resolve, 10000));
      }
    }
    runFetchNodesPeriodically();

    return () => {
      isMounted = false;
    };
  }, []);

  useEffect(() => {
    {
      isLoading
        ? setButtonUpdateText("⏳ Обновляется...")
        : setButtonUpdateText("🔄 Обновить");
    }
  }, [isLoading]);

  async function fetchNodes() {
    setIsLoading(true);
    setError(null);
    setNodes([]);

    try {
      const res = await fetch("/api/monitor/nodes");
      if (res.status === 404) {
        setError("Список узлов пуст!");
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
      <div className={depth > 0 ? "replica" : ""} key={node.id}>
        {depth === 0 ? (
          <div>
            <button
              class="delete"
              onClick={() => handleDeleteNode(node.name, false)}
            >
              Удалить
            </button>{" "}
            <button
              class="delete"
              onClick={() => handleDeleteNode(node.name, true)}
            >
              Удалить вместе с данными
            </button>
          </div>
        ) : (
          ""
        )}
        <h2>{node.name}</h2>
        <p>
          <strong>Статус:</strong> {getNodeStatus(node.status)}
          {node.status}
        </p>
        <p>
          <strong>Кол-во элементов:</strong> {node.items.length}
        </p>
        <p>
          <strong>ID:</strong> {node.id}
        </p>
        <p>
          <strong>URL:</strong> {node.url}
        </p>
        <div>
          <strong>Кэш:</strong> {node.items.length === 0 ? "пусто" : ""}
          <ul>
            {node.items.map((item, i) => (
              <li key={i}>
                <div>
                  <strong>Ключ:</strong> {item.key}
                </div>
                <div>
                  <strong>Значение:</strong> {JSON.stringify(item.value)}
                </div>
                <div>
                  <strong>Создан:</strong> {formatDate(item.createdAt)}
                </div>
                <div>
                  <strong>Последний доступ:</strong>{" "}
                  {formatDate(item.lastAccessed)}
                </div>
                {item.ttl && (
                  <div>
                    <strong>TTL:</strong> {item.ttl}
                  </div>
                )}
              </li>
            ))}
          </ul>
        </div>
        {node.replicas?.length > 0 && (
          <div>
            <strong>Реплики:</strong>
            {node.replicas.map((r) => renderNodeDetails(r, depth + 1))}
          </div>
        )}
      </div>
    );
  }

  const handleDeleteNode = async (nodeName, isForce) => {
    try {
      const res = await fetch(
        `api/cluster/nodes/delete/${encodeURIComponent(nodeName)}${
          isForce ? "?force=true" : ""
        }`,
        {
          method: "DELETE",
        }
      );

      if (!res.ok) throw new Error("Ошибка при удалении");

      const result = await res.json();
      console.log("Удалено:", result);
      await fetchNodes();
      await setSelectedNode(null);
    } catch (error) {
      console.error("Ошибка:", error);
    }
  };

  function formatDate(isoString) {
    if (!isoString) return "-";
    const date = new Date(isoString);
    return date.toLocaleString("ru-RU", {
      year: "numeric",
      month: "numeric",
      day: "numeric",
      hour: "2-digit",
      minute: "2-digit",
      second: "2-digit",
    });
  }

  const getNodeStatus = (status) => {
    switch (status) {
      case "Online":
        return "🟢";
      case "Offline":
        return "⚫️";
      case "Initializing":
        return "🟠";
      case "Error":
        return "🔴";
      default:
        return "❓";
    }
  };

  const onAddNode = async (nodeName, copiesCount) => {
    if (nodeName.length > 0 && copiesCount > 0 && copiesCount <= 10) {
      console.log("Добавление ноды");
      try {
        const res = await fetch(
          `api/cluster/nodes/create/${encodeURIComponent(
            nodeName
          )}/${encodeURIComponent(copiesCount)}`,
          {
            method: "POST",
          }
        );

        if (!res.ok) throw new Error("Ошибка при добавлении узла");

        const result = await res.json();
        console.log("Добавление узла прошло успешно:", result);
        await fetchNodes();
        await setSelectedNode(null);
      } catch (error) {
        console.error("Ошибка:", error);
      }
    }
  };

  const centerX = 300;
  const centerY = 300;
  const total = nodes.length + 1;
  const baseRadius = 200;
  const radius = baseRadius + Math.max(0, total - 12) * 10;

  return (
    <>
      <div className="circle-container">
        <button
          className="refresh-button"
          disabled={isLoading}
          onClick={fetchNodes}
          style={{
            left: `${centerX}px`,
            top: `${centerY}px`,
            zIndex: 2,
            padding: "10px 20px",
            borderRadius: "8px",
            background: isLoading ? "#ccc" : "#007bff",
            color: isLoading ? "#666" : "white",
            cursor: isLoading ? "not-allowed" : "pointer",
            boxShadow: isLoading ? "none" : "0 2px 6px rgba(0, 0, 0, 0.2)",
          }}
        >
          {buttonUpdateNodesText}
        </button>
        {error && (
          <p
            style={{
              color: "black",
              position: "absolute",
              top: `calc(${centerY}px + 40px)`,
              left: `${centerX}px`,
              transform: "translateX(-50%)",
              zIndex: 1,
              margin: 0,
              fontWeight: "bold",
            }}
          >
            {error}
          </p>
        )}
        {[...nodes, { isEmpty: true }].map((node, index) => {
          const angle = ((2 * Math.PI) / total) * index - Math.PI / 2;
          const x = centerX + radius * Math.cos(angle);
          const y = centerY + radius * Math.sin(angle);

          if (node.isEmpty) {
            return (
              <div
                className="node add-node"
                key="node-add"
                style={{ left: `${x}px`, top: `${y}px` }}
                onClick={() => setIsAddOpen(true)}
              >
                <h2>Добавить ноду</h2>
              </div>
            );
          }

          return (
            <div
              className="node"
              key={node.id}
              style={{ left: `${x}px`, top: `${y}px` }}
              onClick={() => setSelectedNode(node)}
            >
              <p>
                {getNodeStatus(node.status)} Статус: {node.status}
              </p>
              <h3>{node.name}</h3>
              <small>
                <strong>Кол-во элементов:</strong> {node.items.length}
              </small>
              <br />
              <small>
                <strong>ID:</strong> {node.id.slice(0, 6)}...
              </small>
              <br />
              <small>
                <strong>URL:</strong> {node.url.slice(0, 7)}...
              </small>
            </div>
          );
        })}
      </div>

      {selectedNode && (
        <div className="modal-overlay" onClick={() => setSelectedNode(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <span className="close-btn" onClick={() => setSelectedNode(null)}>
              &times;
            </span>
            <div>{renderNodeDetails(selectedNode)}</div>
          </div>
        </div>
      )}

      {isAddOpen && (
        <div className="modal-overlay" onClick={() => setIsAddOpen(false)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <span className="close-btn" onClick={() => setIsAddOpen(false)}>
              &times;
            </span>

            <h2>Создать ноду</h2>
            <label>
              Имя контейнера:
              <input
                type="text"
                value={containerName}
                onChange={(e) => setContainerName(e.target.value)}
              />
            </label>
            <label>
              Количество копий:
              <input
                type="number"
                min="1"
                value={copies}
                onChange={(e) => setCopies(Number(e.target.value))}
              />
            </label>
            <div className="modal-buttons">
              <button
                onClick={() => {
                  onAddNode(containerName, copies);
                  setIsAddOpen(false);
                  setContainerName("");
                  setCopies(1);
                }}
              >
                Создать
              </button>
              <button onClick={() => setIsAddOpen(false)}>Отмена</button>
            </div>
          </div>
        </div>
      )}
    </>
  );
}

export default CircleNodes;
