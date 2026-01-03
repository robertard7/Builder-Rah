import React, { useMemo, useState } from 'react';
import Highlight, { defaultProps } from 'prism-react-renderer';
import theme from 'prism-react-renderer/themes/nightOwl';

function CodeBlock({ code, language = 'javascript' }) {
  return (
    <Highlight {...defaultProps} theme={theme} code={code || ''} language={language}>
      {({ className, style, tokens, getLineProps, getTokenProps }) => (
        <pre className={className} style={{ ...style, padding: '12px', borderRadius: 10, overflowX: 'auto' }}>
          {tokens.map((line, i) => (
            <div key={i} {...getLineProps({ line, key: i })}>
              {line.map((token, key) => (
                <span key={key} {...getTokenProps({ token, key })} />
              ))}
            </div>
          ))}
        </pre>
      )}
    </Highlight>
  );
}

function TreeView({ node }) {
  if (!node) return null;
  return (
    <ul>
      <li>
        <strong>{node.name}</strong>
      </li>
      {Array.isArray(node.children) && (
        <ul>
          {node.children.map((child) => (
            <li key={child.path}>
              {child.type === 'file' ? child.name : <TreeView node={child} />}
            </li>
          ))}
        </ul>
      )}
    </ul>
  );
}

function OutputCards({ cards, status }) {
  const [selectedCode, setSelectedCode] = useState(null);

  const renderedCards = useMemo(() => {
    return (cards || []).map((card, index) => {
      const key = card.id || `card-${index}`;
      const type = card.type || 'text';
      if (type === 'code') {
        return (
          <div key={key} className="card">
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <strong>{card.title || 'Code'}</strong>
              <button className="button secondary" onClick={() => setSelectedCode(card)}>
                Preview
              </button>
            </div>
            <pre className="code-preview" style={{ maxHeight: 160, overflow: 'auto' }}>
              {card.language ? `${card.language}\n` : ''}
              {card.content || card.code}
            </pre>
          </div>
        );
      }
      if (type === 'tree') {
        return (
          <div key={key} className="card">
            <strong>{card.title || 'Tree'}</strong>
            <TreeView node={card.tree || card.content} />
          </div>
        );
      }
      if (type === 'error') {
        return (
          <div key={key} className="card" style={{ borderColor: '#fca5a5' }}>
            <strong style={{ color: '#dc2626' }}>Error</strong>
            <div>{card.message || card.content}</div>
          </div>
        );
      }
      if (type === 'clarification') {
        return (
          <div key={key} className="card" style={{ borderColor: '#fbbf24' }}>
            <strong>Pending Clarification</strong>
            <div>{card.prompt || card.question || card.content}</div>
          </div>
        );
      }
      return (
        <div key={key} className="card">
          <div>{card.title || 'Message'}</div>
          <div>{card.content || card.text}</div>
        </div>
      );
    });
  }, [cards]);

  return (
    <div>
      <div className="output-cards">{renderedCards}</div>
      {status === 'loading' && <div className="status-bar">Loading latest output…</div>}
      {status === 'connecting' && <div className="status-bar">Connecting to stream…</div>}
      {selectedCode && (
        <div className="modal-overlay" onClick={() => setSelectedCode(null)}>
          <div className="modal" onClick={(e) => e.stopPropagation()}>
            <h3>{selectedCode.title || 'Code Preview'}</h3>
            <CodeBlock code={selectedCode.content || selectedCode.code} language={selectedCode.language || 'javascript'} />
          </div>
        </div>
      )}
    </div>
  );
}

export default OutputCards;
