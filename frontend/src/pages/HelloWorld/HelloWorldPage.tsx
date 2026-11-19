import React, { useEffect, useState } from 'react';
import './HelloWorldPage.css';

interface Me {
  name?: string;
  email?: string;
}

const HelloWorldPage: React.FC = () => {
  const [me, setMe] = useState<Me | null>(null);

  useEffect(() => {
    fetch('/api/auth/me')
      .then((res) => (res.ok ? res.json() : null))
      .then((data) => setMe(data))
      .catch(() => setMe(null));
  }, []);

  return (
    <div className="page-wrapper hello-page">
      <form className="hello-card" onSubmit={(e) => e.preventDefault()}>
        <h1>Hello World</h1>
        <label htmlFor="hello-user">Usuário logado</label>
        <input id="hello-user" type="text" readOnly value={me?.name ?? 'Carregando...'} />
        {me?.email && <p className="hello-email">{me.email}</p>}
      </form>
    </div>
  );
};

export default HelloWorldPage;
