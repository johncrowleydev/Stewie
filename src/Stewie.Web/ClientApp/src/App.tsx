/**
 * App — Root component with route definitions.
 * Wraps all pages in the Layout shell component.
 */
import { Routes, Route } from "react-router-dom";
import { Layout } from "./components/Layout";
import { DashboardPage } from "./pages/DashboardPage";
import { RunsPage } from "./pages/RunsPage";
import { RunDetailPage } from "./pages/RunDetailPage";
import { ProjectsPage } from "./pages/ProjectsPage";
import { EventsPage } from "./pages/EventsPage";

function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/runs" element={<RunsPage />} />
        <Route path="/runs/:id" element={<RunDetailPage />} />
        <Route path="/projects" element={<ProjectsPage />} />
        <Route path="/events" element={<EventsPage />} />
      </Route>
    </Routes>
  );
}

export default App;
