import React, { ReactNode } from 'react';
import './DataGrid.css';

export interface Column<T> {
  header: string;
  accessor: keyof T | ((row: T) => ReactNode);
  width?: string;
}

interface DataGridProps<T> {
  data: T[];
  columns: Column<T>[];
  keyExtractor: (item: T) => string;
  actions?: (item: T) => ReactNode;
}

export function DataGrid<T>({ data, columns, keyExtractor, actions }: DataGridProps<T>) {
  return (
    <div className="grid-container">
      <div className="grid-table-wrapper">
        <table className="grid-table">
          <thead>
            <tr>
              {columns.map((col, index) => (
                <th key={index} style={{ width: col.width }}>{col.header}</th>
              ))}
              {actions && <th className="actions-header">Ações</th>}
            </tr>
          </thead>
          <tbody>
            {data.length === 0 ? (
              <tr>
                <td colSpan={columns.length + (actions ? 1 : 0)} className="grid-empty">
                  Nenhum registro encontrado.
                </td>
              </tr>
            ) : (
              data.map((row) => (
                <tr key={keyExtractor(row)}>
                  {columns.map((col, index) => (
                    <td key={index}>
                      {typeof col.accessor === 'function' ? col.accessor(row) : (row[col.accessor] as any)}
                    </td>
                  ))}
                  {actions && (
                    <td className="actions-cell">
                      {actions(row)}
                    </td>
                  )}
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
      <div className="grid-pagination">
        <span>Mostrando {data.length} de {data.length} registros</span>
        <div className="pagination-controls">
           <button className="btn-secondary" disabled>Anterior</button>
           <button className="btn-secondary" disabled>Próxima</button>
        </div>
      </div>
    </div>
  );
}
