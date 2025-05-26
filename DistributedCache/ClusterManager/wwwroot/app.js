const baseUrl = "https://localhost:7275";

function switchTab(tabName) {
    const isManager = tabName === "manager";
    document.getElementById("managerTab").classList.toggle("hidden", !isManager);
    document.getElementById("monitorTab").classList.toggle("hidden", isManager);
    document.getElementById("managerMenu").classList.toggle("hidden", !isManager);
    document.getElementById("monitorMenu").classList.toggle("hidden", isManager);
}

// --- Manager actions ---
async function getCache() {
    const key = document.getElementById('getKey').value.trim();
    if (!key) return showToast("Введите ключ для получения кеша.");

    try {
        const res = await fetch(`${baseUrl}/api/cluster/cache/${encodeURIComponent(key)}`);
        if (!res.ok) throw new Error(`Ошибка ${res.status}`);
        document.getElementById('getResult').textContent = JSON.stringify(await res.json(), null, 2);
    } catch (err) {
        showToast("Ошибка при получении кеша: " + err.message);
    }
}

async function setCache() {
    const key = document.getElementById('setKey').value.trim();
    const value = document.getElementById('setValue').value.trim();
    const ttl = document.getElementById('setTTL').value;

    if (!key || !value) return showToast("Введите ключ и значение для установки кеша.");

    const body = {
        key,
        value,
        ttl: ttl ? `PT${ttl}S` : null
    };

    try {
        const res = await fetch(`${baseUrl}/api/cluster/cache`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(body)
        });
        if (!res.ok) throw new Error(`Ошибка ${res.status}`);
        document.getElementById('setResult').textContent = "Успешно";
    } catch (err) {
        showToast("Ошибка при установке кеша: " + err.message);
    }
}

async function createNode() {
    const name = document.getElementById('nodeName').value.trim();
    const count = document.getElementById('copiesCount').value;

    if (!name || !count) return showToast("Введите имя контейнера и количество копий.");

    try {
        const res = await fetch(`${baseUrl}/api/cluster/nodes/create/${encodeURIComponent(name)}/${count}`, {
            method: 'POST'
        });
        if (!res.ok) throw new Error(`Ошибка ${res.status}`);
        document.getElementById('createResult').textContent = await res.text();
    } catch (err) {
        showToast("Ошибка при создании узла: " + err.message);
    }
}

async function deleteNode() {
    const name = document.getElementById('deleteName').value.trim();
    const force = document.getElementById('forceDelete').checked;

    if (!name) return showToast("Введите имя контейнера для удаления.");

    try {
        const res = await fetch(`${baseUrl}/api/cluster/nodes/delete/${encodeURIComponent(name)}?force=${force}`, {
            method: 'DELETE'
        });
        if (!res.ok) throw new Error(`Ошибка ${res.status}`);
        document.getElementById('deleteResult').textContent = await res.text();
    } catch (err) {
        showToast("Ошибка при удалении узла: " + err.message);
    }
}

// --- Monitor actions ---
let nodeElements = [];
let currentIndex = 0;

async function getAllNodesData() {
    try {
        const res = await fetch(`${baseUrl}/api/monitor/nodes`);
        if (!res.ok) throw new Error(`Ошибка ${res.status}`);
        const data = await res.json();

        const container = document.getElementById("allNodesDataResult");
        container.innerHTML = "";
        nodeElements = [];
        currentIndex = 0;

        Object.entries(data).forEach(([nodeName, nodeData]) => {
            const card = document.createElement("div");
            card.className = "node-full-card";

            const nameEl = document.createElement("div");
            nameEl.textContent = nodeName;
            nameEl.style.fontWeight = "bold";
            nameEl.style.marginBottom = "10px";
            nameEl.style.color = "black";

            const dataEl = document.createElement("pre");
            dataEl.textContent = JSON.stringify(nodeData, null, 2);
            dataEl.style.color = "black";

            card.appendChild(nameEl);
            card.appendChild(dataEl);

            container.appendChild(card);
            nodeElements.push(card);
        });

        updateCarousel();
    } catch (err) {
        showToast("Ошибка при получении всех данных узлов: " + err.message);
    }
}


function updateCarousel() {
    // Снимаем выделение с всех карточек
    nodeElements.forEach(card => card.classList.remove("center"));

    const total = nodeElements.length;
    if (total === 0) return;

    const leftIndex = (currentIndex - 1 + total) % total;
    const rightIndex = (currentIndex + 1) % total;

    // Скрываем все карточки
    nodeElements.forEach(card => {
        card.style.display = 'none';
    });

    // Показываем три нужные карточки
    nodeElements[leftIndex].style.display = 'block';
    nodeElements[currentIndex].style.display = 'block';
    nodeElements[rightIndex].style.display = 'block';

    // Добавляем класс center центральной карточке
    nodeElements[currentIndex].classList.add("center");
}

document.getElementById('btnLeft').addEventListener('click', () => {
    currentIndex = (currentIndex - 1 + nodeElements.length) % nodeElements.length;
    updateCarousel();
});

document.getElementById('btnRight').addEventListener('click', () => {
    currentIndex = (currentIndex + 1) % nodeElements.length;
    updateCarousel();
});


async function getSingleNodeData() {
    const key = document.getElementById('monitorNodeKey').value.trim();
    if (!key) return showToast("Введите ключ узла для просмотра данных.");

    try {
        const res = await fetch(`${baseUrl}/api/monitor/node/${encodeURIComponent(key)}`);
        if (!res.ok) throw new Error(`Ошибка ${res.status}`);
        document.getElementById('nodeDataResult').textContent = JSON.stringify(await res.json(), null, 2);
    } catch (err) {
        showToast("Ошибка при получении данных узла: " + err.message);
    }
}

async function getAllNodesStatus() {
    try {
        const res = await fetch(`${baseUrl}/api/monitor/nodes/status`);
        if (!res.ok) throw new Error(`Ошибка ${res.status}`);
        document.getElementById('allNodesStatusResult').textContent = JSON.stringify(await res.json(), null, 2);
    } catch (err) {
        showToast("Ошибка при получении статуса всех узлов: " + err.message);
    }
}

async function getSingleNodeStatus() {
    const key = document.getElementById('statusNodeKey').value.trim();
    if (!key) return showToast("Введите ключ узла для просмотра статуса.");

    try {
        const res = await fetch(`${baseUrl}/api/monitor/node/status/${encodeURIComponent(key)}`);
        if (!res.ok) throw new Error(`Ошибка ${res.status}`);
        document.getElementById('nodeStatusResult').textContent = JSON.stringify(await res.json(), null, 2);
    } catch (err) {
        showToast("Ошибка при получении статуса узла: " + err.message);
    }
}

// --- Theme toggle ---
function toggleTheme() {
    document.body.classList.toggle("dark");
}

// --- Toast handler ---
function showToast(message) {
    const toast = document.getElementById("toast");
    toast.textContent = message;
    toast.classList.remove("hidden");
    toast.classList.add("show");

    setTimeout(() => {
        toast.classList.remove("show");
        setTimeout(() => {
            toast.classList.add("hidden");
        }, 300);
    }, 4000);
}
