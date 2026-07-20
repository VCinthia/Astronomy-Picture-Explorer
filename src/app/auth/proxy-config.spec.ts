import proxyConfig from '../../../proxy.conf.json';

describe('development same-origin proxy', () => {
  it('routes application API and auth paths to the local backend without changing origin', () => {
    expect(proxyConfig['/api']).toEqual({
      target: 'http://localhost:5179',
      secure: false,
      changeOrigin: false
    });
    expect(proxyConfig['/auth']).toEqual(proxyConfig['/api']);
  });
});
