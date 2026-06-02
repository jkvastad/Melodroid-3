import type {ReactNode} from 'react';
import Layout from '@theme/Layout';
import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';

export default function Home(): ReactNode {
  const {siteConfig} = useDocusaurusContext();
  return (
    <Layout title={siteConfig.title} description={siteConfig.tagline}>
      <main style={{maxWidth: 760, margin: '0 auto', padding: '4rem 1rem'}}>
        <h1>{siteConfig.title}</h1>
        <p style={{fontSize: '1.2rem'}}>{siteConfig.tagline}</p>        
        <Link className="button button--primary button--lg" to="/docs/intro">
          Read the theory →
        </Link>
      </main>
    </Layout>
  );
}
