function normalizeEnvName(name) {
    return (name || '').trim().toLowerCase();
}

function stageBucketKey(name, buckets) {
    const normalized = normalizeEnvName(name);
    if (!normalized || !Array.isArray(buckets))
        return null;

    for (const bucket of buckets) {
        const nombres = bucket.nombres || [];
        if (nombres.some(n => normalizeEnvName(n) === normalized)) {
            const key = (bucket.key || '').trim().toLowerCase();
            return key || null;
        }
    }

    return null;
}

function findEnvCell(prId, env, buckets) {
    if (!prId || !env)
        return null;

    const byId = document.getElementById(`statusP-${prId}-${env.definitionEnvironmentId}`);
    if (byId)
        return byId;

    const envName = env.name || '';
    const cells = document.querySelectorAll(`td.statusP-cell[data-idpr="${prId}"]`);
    const envKey = stageBucketKey(envName, buckets);
    if (envKey) {
        for (const cell of cells) {
            if (stageBucketKey(cell.dataset.envName, buckets) === envKey)
                return cell;
        }
    }

    const literal = normalizeEnvName(envName);
    if (literal) {
        for (const cell of cells) {
            if (normalizeEnvName(cell.dataset.envName) === literal)
                return cell;
        }
    }

    return null;
}
