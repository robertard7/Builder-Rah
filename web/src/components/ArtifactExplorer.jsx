import React, { useEffect, useState } from 'react';
import clsx from 'clsx';

function ArtifactExplorer({ sessionToken, tree, loading, pushToast }) {
  const [expanded, setExpanded] = useState(new Set());
  const [preview, setPreview] = useState(null);

  useEffect(() => {
    setExpanded(new Set());
    setPreview(null);
  }, [sessionToken]);

  const toggleNode = (path) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(path)) {
        next.delete(path);
      } else {
        next.add(path);
      }
      return next;
    });
  };

  const fetchContent = async (node) => {
    if (!sessionToken || !node || node.type !== 'file') return;
    try {
      const response = await fetch(
        `/api/artifacts/content?session=${encodeURIComponent(sessionToken)}&path=${encodeURIComponent(node.path || node.name)}`
      );
      if (!response.ok) {
        throw new Error('Unable to load artifact content');
      }
      const text = await response.text();
      setPreview({ name: node.name, content: text });
    } catch (err) {
      pushToast?.(err.message, 'error');
    }
  };

  const downloadZip = async () => {
    if (!sessionToken) return;
    try {
      const hashParam = tree?.hash ? `&hash=${encodeURIComponent(tree.hash)}` : '';
      const response = await fetch(`/api/artifacts/download?session=${encodeURIComponent(sessionToken)}${hashParam}`);
      if (!response.ok) {
        throw new Error('Failed to download artifacts');
      }
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = 'artifacts.zip';
      link.click();
      window.URL.revokeObjectURL(url);
    } catch (err) {
      pushToast?.(err.message, 'error');
    }
  };

  const renderNode = (node) => {
    if (!node) return null;
    const isFolder = node.type === 'directory' || Array.isArray(node.children);
    const isOpen = expanded.has(node.path || node.name);

    return (
      <li key={node.path || node.name}>
        <button className={clsx('tab-button', { active: isOpen })} onClick={() => (isFolder ? toggleNode(node.path || node.name) : fetchContent(node))}>
          {isFolder ? (isOpen ? '📂' : '📁') : '📄'} {node.name}
        </button>
        {isFolder && isOpen && node.children && (
          <ul>
            {node.children.map((child) => (
              <React.Fragment key={child.path || child.name}>{renderNode(child)}</React.Fragment>
            ))}
          </ul>
        )}
      </li>
    );
  };

  return (
    <div>
      <div className="controls" style={{ marginBottom: 8 }}>
        <button className="button secondary" onClick={downloadZip} disabled={!sessionToken}>
          Download ZIP
        </button>
      </div>
      {loading && <div>Loading artifacts…</div>}
      {!loading && tree && <div className="artifact-tree">{renderNode(tree)}</div>}
      {!loading && !tree && <div>No artifacts yet.</div>}
      {preview && (
        <div style={{ marginTop: 12 }}>
          <div className="section-title">
            <strong>Preview: {preview.name}</strong>
            <button className="button secondary" onClick={() => setPreview(null)}>
              Close
            </button>
          </div>
          <pre className="code-preview">{preview.content}</pre>
        </div>
      )}
    </div>
  );
}

export default ArtifactExplorer;
